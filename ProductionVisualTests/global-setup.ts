import { type FullConfig } from '@playwright/test';
import { spawn, execFileSync, type ChildProcess } from 'node:child_process';
import { once } from 'node:events';
import { mkdtempSync, readdirSync, rmSync } from 'node:fs';
import { createServer } from 'node:net';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { setTimeout as delay } from 'node:timers/promises';

const root = path.resolve(__dirname, '..');
const project = path.join(root, 'KazdyDzienZJezusem/KazdyDzienZJezusem.csproj');

async function availablePort(port = 0): Promise<number> {
  const server = createServer();
  server.listen(port, '127.0.0.1');
  await once(server, 'listening');
  const allocated = (server.address() as { port: number }).port;
  await new Promise<void>((resolve, reject) => server.close(error => error ? reject(error) : resolve()));
  return allocated;
}

export default async function setup(config: FullConfig) {
  const baseURL = config.projects[0].use.baseURL!;
  // Never reuse a server that might be running an older checkout.
  await availablePort(Number(new URL(baseURL).port));
  const redisPort = await availablePort();
  const redisServer = process.env.KDJ_REDIS_SERVER
    ?? path.join(root, 'artifacts/visual-tools/redis-8.2.2/src/redis-server');
  const redisCli = process.env.KDJ_REDIS_CLI ?? path.join(path.dirname(redisServer), 'redis-cli');
  const redisDirectory = mkdtempSync(path.join(tmpdir(), 'kdj-visual-redis-'));
  const children: ChildProcess[] = [];
  let stopping: Promise<void> | undefined;
  let childFailure: Error | undefined;

  function start(command: string, args: string[], env = process.env) {
    const child = spawn(command, args, { cwd: root, env, stdio: ['ignore', 'pipe', 'pipe'] });
    children.push(child);
    child.stdout!.pipe(process.stdout);
    child.stderr!.pipe(process.stderr);
    child.on('error', error => { childFailure = error; });
    child.on('exit', (code, signal) => {
      if (!stopping) childFailure = new Error(`${command} exited unexpectedly (${signal ?? code}).`);
    });
    return child;
  }

  function stop() {
    return stopping ??= (async () => {
      for (const child of [...children].reverse()) {
        if (child.exitCode !== null || child.signalCode !== null || !child.pid) continue;
        const exited = once(child, 'exit');
        child.kill('SIGTERM');
        const timer = setTimeout(() => child.kill('SIGKILL'), 5_000);
        try { await exited; } finally { clearTimeout(timer); }
      }
      rmSync(redisDirectory, { recursive: true, force: true });
    })();
  }

  const onInterrupt = () => { void stop().finally(() => process.exit(130)); };
  process.once('SIGINT', onInterrupt);
  process.once('SIGTERM', onInterrupt);

  function redis(...args: string[]) {
    return execFileSync(redisCli, ['-h', '127.0.0.1', '-p', String(redisPort), '--raw', ...args], {
      encoding: 'utf8', timeout: 5_000, stdio: ['ignore', 'pipe', 'pipe'],
    }).trim();
  }

  try {
    const environment = {
      ...process.env,
      DOTNET_CLI_HOME: process.env.DOTNET_CLI_HOME ?? path.join(root, 'artifacts/dotnet-home'),
      DOTNET_NOLOGO: '1',
      DOTNET_CLI_TELEMETRY_OPTOUT: '1',
    };
    execFileSync('dotnet', ['build', project, '-c', 'Release', '--no-restore'], {
      cwd: root, env: environment, stdio: 'inherit', timeout: 120_000,
    });

    // This instance owns an empty directory and never reads or writes dump.rdb.
    const redisProcess = start(redisServer, [
      '--bind', '127.0.0.1', '--port', String(redisPort),
      '--dir', redisDirectory, '--save', '', '--appendonly', 'no',
      '--daemonize', 'no', '--protected-mode', 'yes',
    ]);
    await new Promise<void>((resolve, reject) => {
      const timer = setTimeout(() => reject(new Error('Redis did not start within 15 seconds.')), 15_000);
      let output = '';
      redisProcess.stdout!.on('data', chunk => {
        output += chunk;
        if (output.includes('Ready to accept connections')) { clearTimeout(timer); resolve(); }
      });
      redisProcess.once('error', error => { clearTimeout(timer); reject(error); });
      redisProcess.once('exit', () => { clearTimeout(timer); reject(new Error('Redis startup failed.')); });
    });
    if (redis('DBSIZE') !== '0') throw new Error('The test Redis instance must start empty.');

    start('dotnet', [
      'run', '--project', project, '-c', 'Release', '--no-build', '--no-restore',
      '--no-launch-profile', '--urls', baseURL,
    ], {
      ...environment,
      ASPNETCORE_ENVIRONMENT: 'Development',
      DOTNET_ENVIRONMENT: 'Development',
      Tags__RedisConnection: `127.0.0.1:${redisPort},abortConnect=true`,
    });
    const deadline = Date.now() + 60_000;
    while (true) {
      if (childFailure) throw childFailure;
      const response = await fetch(`${baseURL}/Biblia`, { signal: AbortSignal.timeout(2_000) }).catch(() => null);
      if (response?.status === 200) break;
      if (Date.now() > deadline) throw new Error('The application did not become ready within 60 seconds.');
      await delay(250);
    }

    // Program.cs seeds the very same tag catalog as the Compose application.
    const tagFiles = readdirSync(path.join(root, 'tags')).filter(file => file.endsWith('.md'));
    if (Number(redis('HLEN', 'tags:definitions')) !== tagFiles.length || tagFiles.length === 0
      || Number(redis('HLEN', 'tags:fragments')) === 0) {
      throw new Error('Application startup did not populate the Redis tag catalog.');
    }
    for (const file of tagFiles) {
      if (redis('HEXISTS', 'tags:definitions', path.basename(file, '.md')) !== '1') {
        throw new Error(`Missing Redis definition for ${file}.`);
      }
    }
    console.log(`Verified ${tagFiles.length} tag definitions and the fragment index in isolated Redis.`);
  } catch (error) {
    await stop();
    process.removeListener('SIGINT', onInterrupt);
    process.removeListener('SIGTERM', onInterrupt);
    throw error;
  }

  return async () => {
    try {
      if (childFailure) throw childFailure;
      if (redis('PING') !== 'PONG') throw new Error('Redis stopped during the screenshot tests.');
    } finally {
      await stop();
      process.removeListener('SIGINT', onInterrupt);
      process.removeListener('SIGTERM', onInterrupt);
    }
  };
}
