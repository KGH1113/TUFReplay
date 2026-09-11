if redis.call('EXISTS', KEYS[1]) == 1 then
  return 0
end
redis.call('HSET', KEYS[1],
  'status', 'created',
  'token_hash', ARGV[1],
  'next_sequence', 0,
  'total_bytes', 0,
  'hard_expires_at', ARGV[2])
if ARGV[4] and ARGV[4] ~= '' then redis.call('HSET', KEYS[1], 'owner_id', ARGV[4]) end
redis.call('EXPIRE', KEYS[1], ARGV[3])
return 1
