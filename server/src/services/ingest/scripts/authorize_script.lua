if redis.call('EXISTS', KEYS[1]) == 0 then return -1 end
if redis.call('HGET', KEYS[1], 'token_hash') ~= ARGV[1] then return -2 end
local status = redis.call('HGET', KEYS[1], 'status')
if status == 'sealed' then return 1 end
if status ~= 'created' and status ~= 'streaming' then return -3 end
local now = tonumber(redis.call('TIME')[1])
if now >= tonumber(redis.call('HGET', KEYS[1], 'hard_expires_at')) then return -4 end
return 1
