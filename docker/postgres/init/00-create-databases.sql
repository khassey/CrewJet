-- Creates the databases required by CrewJet.Auth and CrewJet.Server on first container init.
-- This script only runs once (when the named volume is first created).
-- It is safe to re-run; existing databases are left untouched.

SELECT 'CREATE DATABASE crewjet'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'crewjet')\gexec

SELECT 'CREATE DATABASE openiddict'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'openiddict')\gexec
