import ijson
import redis
import json

r = redis.Redis(host='localhost', port=6379, decode_responses=True)

pipe = r.pipeline()
BATCH = 1000
count = 0

with open('data/gnt.flat.json', 'rb') as f:
    for item in ijson.items(f, 'item'):
        key = f"gnt:{item['book_name_osis']}:{item['chapter']}:{item['verse']}"
        pipe.set(key, json.dumps(item, ensure_ascii=False))
        count += 1

        if count % BATCH == 0:
            pipe.execute()
            print("Inserted:", count)

pipe.execute()
print("DONE:", count)
