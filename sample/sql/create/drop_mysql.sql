REVOKE ALL PRIVILEGES ON `asyncsql_test` . * FROM 'asyncsql'@'localhost';

REVOKE GRANT OPTION ON `asyncsql_test` . * FROM 'asyncsql'@'localhost';

DROP USER 'asyncsql'@'localhost';

DROP DATABASE IF EXISTS `asyncsql_test`;
