/** SAH Pool logs a noisy virtual-FS error when unlinking a missing name.
 * A cold benchmark deliberately starts without that name, so avoid unlink.
 * Call only while holding the origin-wide storage lock and with VFS unpaused.
 */
export function unlinkSahIfPresent(pool, filename) {
  if (!pool.getFileNames().includes(filename)) return false;
  return pool.unlink(filename);
}
