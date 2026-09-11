if redis.call('EXISTS', KEYS[1]) == 0 then return 0 end
if redis.call('HGET', KEYS[1], 'token_hash') ~= ARGV[1] then return -1 end
if redis.call('HGET', KEYS[1], 'connection_id') ~= ARGV[2] then return -3 end
local status = redis.call('HGET', KEYS[1], 'status')
if status ~= 'created' and status ~= 'streaming' then return -3 end
redis.call('DEL', KEYS[1], KEYS[2])
return 1
