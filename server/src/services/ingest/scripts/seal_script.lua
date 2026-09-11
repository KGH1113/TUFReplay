if redis.call('EXISTS', KEYS[1]) == 0 then return {-1, -1, -1} end
if redis.call('HGET', KEYS[1], 'token_hash') ~= ARGV[1] then return {-2, -1, -1} end
if redis.call('HGET', KEYS[1], 'connection_id') ~= ARGV[6] then return {-6, -1, -1} end
local status = redis.call('HGET', KEYS[1], 'status')
local ack = tonumber(redis.call('HGET', KEYS[1], 'next_sequence')) - 1
local total = tonumber(redis.call('HGET', KEYS[1], 'total_bytes'))
if status == 'sealed' then
  if tonumber(ARGV[2]) ~= ack or redis.call('HGET', KEYS[1], 'input_count') ~= ARGV[3] or redis.call('HGET', KEYS[1], 'hit_context_count') ~= ARGV[4] then return {-6,-1,-1} end
  return {2, ack, total}
end
if tonumber(redis.call('TIME')[1]) >= tonumber(redis.call('HGET', KEYS[1], 'hard_expires_at')) then return {-4,-1,-1} end
if status ~= 'streaming' then return {-3, -1, -1} end
if tonumber(ARGV[2]) ~= ack then return {3, ack + 1, total} end
redis.call('HSET', KEYS[1], 'status', 'sealed', 'input_count', ARGV[3], 'hit_context_count', ARGV[4])
redis.call('EXPIRE', KEYS[1], ARGV[5])
if redis.call('EXISTS', KEYS[2]) == 1 then redis.call('EXPIRE', KEYS[2], ARGV[5]) end
return {1, ack, total}
