if redis.call('EXISTS', KEYS[1]) == 0 then return {-1, -1} end
if redis.call('HGET', KEYS[1], 'token_hash') ~= ARGV[1] then return {-2, -1} end
if redis.call('HGET', KEYS[1], 'status') ~= 'streaming' then return {-3, -1} end
if redis.call('HGET', KEYS[1], 'connection_id') ~= ARGV[8] then return {-6, -1} end
local now = tonumber(redis.call('TIME')[1])
if now >= tonumber(redis.call('HGET', KEYS[1], 'hard_expires_at')) then return {-4, -1} end
local expected = tonumber(redis.call('HGET', KEYS[1], 'next_sequence'))
local sequence = tonumber(ARGV[2])
local digest = ARGV[9]
if sequence < expected then
  if redis.call('HGET', KEYS[1], 'digest:' .. ARGV[2]) ~= digest then return {-6,-1} end
  return {2, expected - 1}
end
if sequence >= 262144 or tonumber(ARGV[5]) == 0 then return {-5,-1} end
if sequence > expected then return {3, expected} end
local total = tonumber(redis.call('HGET', KEYS[1], 'total_bytes')) + tonumber(ARGV[5])
if total > tonumber(ARGV[6]) then return {-5, -1} end
redis.call('XADD', KEYS[2], '*', 'sequence', ARGV[2], 'kind', ARGV[3], 'payload', ARGV[4])
redis.call('HSET', KEYS[1], 'digest:' .. ARGV[2], digest)
redis.call('HSET', KEYS[1], 'next_sequence', expected + 1, 'total_bytes', total)
redis.call('EXPIRE', KEYS[1], ARGV[7])
redis.call('EXPIRE', KEYS[2], ARGV[7])
return {1, sequence}
