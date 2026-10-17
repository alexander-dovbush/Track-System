-- DEV ONLY: one test employee for local testing (never run on a real/production DB)
USE [FinalProject];
GO

-- no row with this userName yet -> insert; already there -> skip (safe to run twice)
IF NOT EXISTS (SELECT 1 FROM dbo.Employees WHERE userName = 'test.employee')
BEGIN
    INSERT INTO dbo.Employees
        (firstName, lastName, email, password, role, userName, status, isFirstLogin, failedLoginAttempts)
    VALUES
        ('Test', 'Employee', 'test.employee@tracksystem.local',
         -- BCrypt hash of Test1234! (the plain password is never stored)
         '$2a$11$/Cxq7g5o60s8SFT..UTR.ezgxeQVJL282D0mdAteMDcdlXodlUaUi',
         -- active + first login -> logging in forces the change-password page
         'Employee', 'test.employee', 'active', 1, 0);
END
GO

-- the test employee -> its eNum is what you type on the Login page
SELECT eNum, userName, email, status, isFirstLogin
FROM dbo.Employees
WHERE userName = 'test.employee';