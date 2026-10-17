-- run against the real DB (partner: same script on his PC)
USE [FinalProject];
GO

-- new table: one row per "forgot password" request
CREATE TABLE dbo.PasswordResetTokens (
    -- row id, auto 1,2,3...
    tokenID   int IDENTITY(1,1) NOT NULL,
    -- employee who asked -> must exist in Employees
    eNum      int      NOT NULL,
    -- SHA-256 of the emailed token, 64 hex chars (real token never stored)
    tokenHash char(64) NOT NULL,
    -- UTC, createdAt + 30 min
    expiresAt datetime NOT NULL,
    -- UTC, NULL = not used yet; set when used OR replaced by a newer link
    usedAt    datetime NULL,
    -- UTC, when the link was created
    createdAt datetime NOT NULL,

    -- tokenID -> primary key
    CONSTRAINT PK_PasswordResetTokens PRIMARY KEY (tokenID),
    -- eNum -> Employees.eNum; employee deleted -> his tokens deleted
    CONSTRAINT FK_PasswordResetTokens_Employees FOREIGN KEY (eNum)
        REFERENCES dbo.Employees (eNum) ON DELETE CASCADE,
    -- same hash twice -> rejected; also makes "find by hash" fast
    CONSTRAINT UQ_PasswordResetTokens_tokenHash UNIQUE (tokenHash)
);
GO