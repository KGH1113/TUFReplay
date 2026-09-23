-- Run as the DB owner after the Rust migration, with ON_ERROR_STOP enabled.
-- psql reads the password from its environment, not a command-line argument.
\getenv admin_password TRUSTED_TESTERS_DB_PASSWORD
SELECT length(:'admin_password') >= 32 AND :'admin_password' ~ '^[A-Za-z0-9_-]+$' AS password_valid \gset
\if :password_valid
BEGIN;
SELECT 'CREATE ROLE trusted_testers_admin LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT'
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'trusted_testers_admin') \gexec
SELECT format('ALTER ROLE trusted_testers_admin WITH LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT PASSWORD %L', :'admin_password') \gexec
SELECT format('GRANT CONNECT ON DATABASE %I TO trusted_testers_admin', current_database()) \gexec
GRANT USAGE ON SCHEMA public TO trusted_testers_admin;
GRANT SELECT, INSERT, UPDATE ON trusted_testers TO trusted_testers_admin;
GRANT SELECT, INSERT ON trusted_tester_events TO trusted_testers_admin;
GRANT USAGE, SELECT ON SEQUENCE trusted_tester_events_id_seq TO trusted_testers_admin;
COMMIT;
\else
\echo TRUSTED_TESTERS_DB_PASSWORD must contain at least 32 URL-safe characters.
DO $$ BEGIN RAISE EXCEPTION 'Invalid administrator database password'; END $$;
\endif
