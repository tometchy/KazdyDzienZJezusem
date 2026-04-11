import ijson
import redis
import json

r = redis.Redis(host='localhost', port=6379, decode_responses=True)

BATCH_SIZE = 1000
pipe = r.pipeline()
count = 0

with open('data/gnt.flat.json', 'rb') as f:
    parser = ijson.items(f, 'item')

    for item in parser:
        key = f"gnt:{item['book_name_osis']}:{item['chapter']}:{item['verse']}"
        
        pipe.set(key, json.dumps(item, ensure_ascii=False))
        count += 1

        if count % BATCH_SIZE == 0:
            pipe.execute()
            print(f"Inserted: {count}")

# flush reszty
pipe.execute()
print(f"Done: {count}")
