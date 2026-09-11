if redis.call('EXISTS', KEYS[1]) == 0 then return {-1, -1} end
if redis.call('HGET', KEYS[1], 'token_hash') ~= ARGV[1] then return {-2, -1} end
if redis.call('HGET', KEYS[1], 'status') ~= 'streaming' then return {-3, -1} end
if redis.call('HGET', KEYS[1], 'connection_id') ~= ARGV[3] then return {-6, -1} end
local now = tonumber(redis.call('TIME')[1])
if now >= tonumber(redis.call('HGET', KEYS[1], 'hard_expires_at')) then return {-4, -1} end
redis.call('EXPIRE', KEYS[1], ARGV[2])
if redis.call('EXISTS', KEYS[2]) == 1 then redis.call('EXPIRE', KEYS[2], ARGV[2]) end
return {1, tonumber(redis.call('HGET', KEYS[1], 'next_sequence')) - 1}
