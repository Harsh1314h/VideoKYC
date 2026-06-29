Imports System.Data
Imports System.Data.SqlClient
Imports Dapper
Imports VideoKYC.Data
Imports VideoKYC.Models

Namespace Services
    Public Class SessionService
        ' ── Customer Registration ───────────────────────────────────────────
        Public Function RegisterCustomer(fullName As String, phone As String) As Customer
            Dim sql As String = "INSERT INTO Customers (FullName, Phone, CreatedAt) " &
                      "VALUES (@Name, @Phone, GETDATE()); " &
                      "SELECT CAST(SCOPE_IDENTITY() as int);"
            
            Using conn As SqlConnection = DatabaseHelper.GetConnection()
                Dim customerId = conn.ExecuteScalar(Of Integer)(sql, New With {.Name = fullName, .Phone = phone})
                Return New Customer With {
                    .CustomerId = customerId,
                    .FullName = fullName,
                    .Phone = phone,
                    .CreatedAt = DateTime.Now
                }
            End Using
        End Function

        ' ── Create KYC Session ──────────────────────────────────────────────
        Public Function CreateSession(customerId As Integer) As String
            Dim sessionId = Guid.NewGuid().ToString()
            Dim sql As String = "INSERT INTO KycSessions (SessionId, CustomerId, Status, CreatedAt, SessionToken) " &
                      "VALUES (@sid, @cid, 'Waiting', GETDATE(), @token)"
            
            Using conn As SqlConnection = DatabaseHelper.GetConnection()
                conn.Execute(sql, New With {
                    .sid = sessionId,
                    .cid = customerId,
                    .token = sessionId
                })
            End Using
            
            LogAudit(sessionId, "Session Created", "Customer registered and session initialized.", "Customer")
            Return sessionId
        End Function

        ' ── Get All Waiting Sessions ────────────────────────────────────────
        Public Function GetWaitingSessions() As IEnumerable(Of KycSession)
            ' 1. Auto-cleanup stale sessions (no heartbeat)
            Dim cleanupSql As String = 
                "UPDATE KycSessions " &
                "SET Status = 'Rejected', RejectionReason = 'Customer left waiting room', UpdatedAt = GETDATE() " &
                "WHERE Status = 'Waiting' " &
                "  AND (" &
                "    (UpdatedAt IS NOT NULL AND DATEDIFF(second, UpdatedAt, GETDATE()) > 30) " &
                "    OR (UpdatedAt IS NULL AND DATEDIFF(second, CreatedAt, GETDATE()) > 30) " &
                "  ); " &
                "UPDATE KycSessions " &
                "SET Status = 'Rejected', RejectionReason = 'Call timed out / abandoned', UpdatedAt = GETDATE() " &
                "WHERE Status = 'InProgress' " &
                "  AND (" &
                "    (UpdatedAt IS NOT NULL AND DATEDIFF(second, UpdatedAt, GETDATE()) > 60) " &
                "    OR (UpdatedAt IS NULL AND DATEDIFF(second, CreatedAt, GETDATE()) > 60) " &
                "  );"

            Dim selectSql As String = "SELECT s.*, c.FullName As CustomerName, c.Phone As CustomerPhone " &
                      "FROM KycSessions s " &
                      "INNER JOIN Customers c ON s.CustomerId = c.CustomerId " &
                      "WHERE s.Status = 'Waiting' " &
                      "ORDER BY s.CreatedAt ASC"
                      
            Using conn As SqlConnection = DatabaseHelper.GetConnection()
                Try
                    conn.Execute(cleanupSql)
                Catch
                    ' Ignore errors if database schema has transient locks
                End Try
                Return conn.Query(Of KycSession)(selectSql)
            End Using
        End Function

        ' ── Get Single Session ──────────────────────────────────────────────
        Public Function GetSession(sessionId As String) As KycSession
            Dim sql As String = "SELECT s.*, c.FullName As CustomerName, c.Phone As CustomerPhone, a.FullName As AgentName " &
                      "FROM KycSessions s " &
                      "INNER JOIN Customers c ON s.CustomerId = c.CustomerId " &
                      "LEFT JOIN Agents a ON s.AgentId = a.AgentId " &
                      "WHERE s.SessionId = @sid"
                      
            Using conn As SqlConnection = DatabaseHelper.GetConnection()
                Return conn.QueryFirstOrDefault(Of KycSession)(sql, New With {.sid = sessionId})
            End Using
        End Function

        ' ── Assign Agent and Start Call ─────────────────────────────────────
        Public Sub AssignAgent(sessionId As String, agentId As Integer)
            Dim sql As String = "UPDATE KycSessions SET AgentId = @aid, Status = 'InProgress', UpdatedAt = GETDATE() " &
                      "WHERE SessionId = @sid"
                      
            Using conn As SqlConnection = DatabaseHelper.GetConnection()
                conn.Execute(sql, New With {.aid = agentId, .sid = sessionId})
            End Using
            
            LogAudit(sessionId, "Agent Joined", "Agent ID " & agentId & " joined the call.", "Agent")
        End Sub

        ' ── Update Session Status ───────────────────────────────────────────
        Public Sub UpdateSessionStatus(sessionId As String, status As String, Optional reason As String = Nothing)
            Dim sql As String = "UPDATE KycSessions SET Status = @s, UpdatedAt = GETDATE(), RejectionReason = @r " &
                      "WHERE SessionId = @sid"
                      
            Using conn As SqlConnection = DatabaseHelper.GetConnection()
                conn.Execute(sql, New With {.s = status, .sid = sessionId, .r = reason})
            End Using
            
            Dim actor = "Agent"
            If status = "Waiting" Then actor = "System"
            LogAudit(sessionId, "Session Status: " & status, "Status changed to " & status & ". Reason: " & If(reason, "N/A"), actor)
        End Sub

        ' ── Logging Audit Action ────────────────────────────────────────────
        Public Sub LogAudit(sessionId As String, action As String, details As String, performedBy As String)
            Dim sql As String = "INSERT INTO KycAuditLog (SessionId, Action, Details, PerformedBy, Timestamp) " &
                      "VALUES (@sid, @act, @det, @by, GETDATE())"
                      
            Using conn As SqlConnection = DatabaseHelper.GetConnection()
                conn.Execute(sql, New With {
                    .sid = sessionId,
                    .act = action,
                    .det = details,
                    .by = performedBy
                })
            End Using
        End Sub

        ' ── Keep Session Alive Heartbeat ────────────────────────────────────
        Public Sub KeepSessionAlive(sessionId As String)
            Dim sql As String = "UPDATE KycSessions SET UpdatedAt = GETDATE() WHERE SessionId = @sid"
            Using conn As SqlConnection = DatabaseHelper.GetConnection()
                conn.Execute(sql, New With {.sid = sessionId})
            End Using
        End Sub

        ' ── Cancel All Active Sessions for a Phone Number ───────────────────
        Public Sub CancelActiveSessionsByPhone(phone As String)
            Dim sql As String = "UPDATE KycSessions " &
                      "SET Status = 'Rejected', RejectionReason = 'Cancelled - New session started', UpdatedAt = GETDATE() " &
                      "FROM KycSessions s " &
                      "INNER JOIN Customers c ON s.CustomerId = c.CustomerId " &
                      "WHERE c.Phone = @Phone AND s.Status IN ('Waiting', 'InProgress')"
            
            Using conn As SqlConnection = DatabaseHelper.GetConnection()
                conn.Execute(sql, New With {.Phone = phone})
            End Using
        End Sub

        Public Function GetActiveSessionsForAgent(agentId As Integer) As IEnumerable(Of KycSession)
            Dim cleanupSql As String = 
                "UPDATE KycSessions " &
                "SET Status = 'Rejected', RejectionReason = 'Call timed out / abandoned', UpdatedAt = GETDATE() " &
                "WHERE Status = 'InProgress' " &
                "  AND (" &
                "    (UpdatedAt IS NOT NULL AND DATEDIFF(second, UpdatedAt, GETDATE()) > 15) " &
                "    OR (UpdatedAt IS NULL AND DATEDIFF(second, CreatedAt, GETDATE()) > 15) " &
                "  );"

            Dim sql As String = "SELECT s.*, c.FullName As CustomerName, c.Phone As CustomerPhone " &
                      "FROM KycSessions s " &
                      "INNER JOIN Customers c ON s.CustomerId = c.CustomerId " &
                      "WHERE s.Status = 'InProgress' AND s.AgentId = @aid " &
                      "ORDER BY s.UpdatedAt DESC"
                      
            Using conn As SqlConnection = DatabaseHelper.GetConnection()
                Try
                    conn.Execute(cleanupSql)
                Catch ex As Exception
                    ' Ignore database errors on auto-cleanup
                End Try
                Return conn.Query(Of KycSession)(sql, New With {.aid = agentId})
            End Using
        End Function

        ' ── Check if Session is Approvable (all verifications passed) ───────
        Public Function CanApproveSession(sessionId As String) As Boolean
            Using conn As SqlConnection = DatabaseHelper.GetConnection()
                ' 1. Check if there is at least one verified document for this session
                Dim docVerified As Boolean = conn.ExecuteScalar(Of Boolean)(
                    "SELECT COALESCE((SELECT TOP 1 IsVerified FROM DocumentVerifications WHERE SessionId = @sid AND IsVerified = 1), 0)",
                    New With {.sid = sessionId}
                )

                ' 2. Check if face is verified for this session
                Dim faceVerified As Boolean = conn.ExecuteScalar(Of Boolean)(
                    "SELECT COALESCE((SELECT TOP 1 IsVerified FROM FaceVerifications WHERE SessionId = @sid AND IsVerified = 1), 0)",
                    New With {.sid = sessionId}
                )

                ' 3. Check if voice is verified for this session
                Dim voiceVerified As Boolean = conn.ExecuteScalar(Of Boolean)(
                    "SELECT COALESCE((SELECT TOP 1 IsVerified FROM VoiceVerifications WHERE SessionId = @sid AND IsVerified = 1), 0)",
                    New With {.sid = sessionId}
                )

                Return docVerified AndAlso faceVerified AndAlso voiceVerified
            End Using
        End Function
    End Class
End Namespace
