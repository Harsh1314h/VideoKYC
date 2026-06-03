-- setup_db.sql
USE master;
GO

IF EXISTS (SELECT * FROM sys.databases WHERE name = 'VideoKYC')
BEGIN
    ALTER DATABASE VideoKYC SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE VideoKYC;
END
GO

CREATE DATABASE VideoKYC;
GO

USE VideoKYC;
GO

-- 1. Customers Table
CREATE TABLE Customers (
    CustomerId INT IDENTITY(1,1) PRIMARY KEY,
    FullName NVARCHAR(150) NOT NULL,
    Phone VARCHAR(20) NOT NULL,
    CreatedAt DATETIME DEFAULT GETUTCDATE()
);
GO

-- 2. Agents Table
CREATE TABLE Agents (
    AgentId INT IDENTITY(1,1) PRIMARY KEY,
    Username VARCHAR(100) NOT NULL UNIQUE,
    PasswordHash VARCHAR(256) NOT NULL,
    FullName NVARCHAR(150) NOT NULL,
    IsActive BIT DEFAULT 1,
    CreatedAt DATETIME DEFAULT GETUTCDATE()
);
GO

-- 3. KycSessions Table
CREATE TABLE KycSessions (
    SessionId VARCHAR(50) PRIMARY KEY,
    CustomerId INT NOT NULL FOREIGN KEY REFERENCES Customers(CustomerId),
    AgentId INT NULL FOREIGN KEY REFERENCES Agents(AgentId),
    Status VARCHAR(20) NOT NULL DEFAULT 'Waiting', -- Waiting, InProgress, Approved, Rejected
    SessionToken VARCHAR(50) NOT NULL,
    CreatedAt DATETIME DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME NULL,
    RejectionReason NVARCHAR(500) NULL
);
GO

-- 4. Document Verifications Table
CREATE TABLE DocumentVerifications (
    DocVerificationId INT IDENTITY(1,1) PRIMARY KEY,
    SessionId VARCHAR(50) NOT NULL FOREIGN KEY REFERENCES KycSessions(SessionId),
    DocumentType VARCHAR(20) NOT NULL, -- Aadhaar, PAN, Passport, DL
    DocumentNumber VARCHAR(50) NULL,
    IsVerified BIT DEFAULT 0,
    ExtractedDataJson NVARCHAR(MAX) NULL,
    ImagePath NVARCHAR(500) NULL,
    OcrText NVARCHAR(MAX) NULL,
    CreatedAt DATETIME DEFAULT GETUTCDATE()
);
GO

-- 5. Face Verifications Table
CREATE TABLE FaceVerifications (
    FaceVerificationId INT IDENTITY(1,1) PRIMARY KEY,
    SessionId VARCHAR(50) NOT NULL FOREIGN KEY REFERENCES KycSessions(SessionId),
    LiveFramePath NVARCHAR(500) NULL,
    DocPhotoPath NVARCHAR(500) NULL,
    ClientScore FLOAT NOT NULL DEFAULT 0,
    ServerScore FLOAT NOT NULL DEFAULT 0,
    IsVerified BIT DEFAULT 0,
    CreatedAt DATETIME DEFAULT GETUTCDATE()
);
GO

-- 6. Voice Verifications Table
CREATE TABLE VoiceVerifications (
    VoiceVerificationId INT IDENTITY(1,1) PRIMARY KEY,
    SessionId VARCHAR(50) NOT NULL FOREIGN KEY REFERENCES KycSessions(SessionId),
    AudioPath NVARCHAR(500) NULL,
    SpokenText NVARCHAR(1000) NULL,
    Phrase NVARCHAR(1000) NULL,
    TextScore FLOAT NOT NULL DEFAULT 0,
    VoiceScore FLOAT NOT NULL DEFAULT 0,
    FinalScore FLOAT NOT NULL DEFAULT 0,
    IsVerified BIT DEFAULT 0,
    CreatedAt DATETIME DEFAULT GETUTCDATE()
);
GO

-- 7. KycAuditLog Table
CREATE TABLE KycAuditLog (
    AuditLogId INT IDENTITY(1,1) PRIMARY KEY,
    SessionId VARCHAR(50) NULL,
    Action NVARCHAR(150) NOT NULL,
    Details NVARCHAR(MAX) NULL,
    PerformedBy NVARCHAR(150) NOT NULL,
    Timestamp DATETIME DEFAULT GETUTCDATE()
);
GO

-- Seed Default Agent: username = agent1, password = password123
-- SHA256 of "password123" is ef92b778bafe771e89245b89ecbc08a44a4e166c06659911881f383d4473e94f
INSERT INTO Agents (Username, PasswordHash, FullName, IsActive)
VALUES ('agent1', 'ef92b778bafe771e89245b89ecbc08a44a4e166c06659911881f383d4473e94f', 'Officer Alice', 1);
GO
