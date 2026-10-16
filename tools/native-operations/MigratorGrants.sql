-- Dedicated migration login only; API and worker roles do not receive these grants.
GRANT CREATE TABLE, CREATE PROCEDURE, CREATE VIEW, CREATE FUNCTION TO m0_migrator;
GRANT ALTER ON SCHEMA::m0 TO m0_migrator;
GRANT ALTER ON SCHEMA::m0commerce TO m0_migrator;
GRANT ALTER ON SCHEMA::ops TO m0_migrator;
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::m0 TO m0_migrator;
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::m0commerce TO m0_migrator;
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::ops TO m0_migrator;
