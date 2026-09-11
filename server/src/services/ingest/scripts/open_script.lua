if redis.call('EXISTS', KEYS[1]) == 0 then return {-1, -1} end
if redis.call('HGET', KEYS[1], 'token_hash') ~= ARGV[1] then return {-2, -1} end
local status = redis.call('HGET', KEYS[1], 'status')
if status ~= 'created' and status ~= 'streaming' then return {-3, -1} end
local now = tonumber(redis.call('TIME')[1])
if now >= tonumber(redis.call('HGET', KEYS[1], 'hard_expires_at')) then return {-4, -1} end
local owner = redis.call('HGET', KEYS[1], 'owner_id')
if owner and owner ~= '' then
  local prefix = string.match(KEYS[1], '^(.-)tufreplay:run:') or ''
  local active = prefix .. 'tufreplay:active:' .. owner
  for _, key in ipairs(redis.call('SMEMBERS', active)) do
    local active_status = redis.call('HGET', key, 'status')
    local deadline = tonumber(redis.call('HGET', key, 'hard_expires_at') or '0')
    if active_status == 'streaming' and deadline > now and key ~= KEYS[1] then
      return {-7, -1}
    end
    redis.call('SREM', active, key)
  end
  redis.call('SADD', active, KEYS[1])
  redis.call('EXPIRE', active, tonumber(redis.call('HGET', KEYS[1], 'hard_expires_at')) - now)
end
redis.call('HSET', KEYS[1], 'status', 'streaming', 'connection_id', ARGV[3])
redis.call('EXPIRE', KEYS[1], ARGV[2])
if redis.call('EXISTS', KEYS[2]) == 1 then redis.call('EXPIRE', KEYS[2], ARGV[2]) end
return {1, tonumber(redis.call('HGET', KEYS[1], 'next_sequence')) - 1}
