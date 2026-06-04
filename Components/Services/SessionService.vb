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
                      "VALUES (@Name, @Phone, GETUTCDATE()); " &
                      "SELECT CAST(SCOPE_IDENTITY() as int);"
            
            Using conn As SqlConnection = DatabaseHelper.GetConnection()
                Dim customerId = conn.ExecuteScalar(Of Integer)(sql, New With {.Name = fullName, .Phone = phone})
                Return New Customer With {
                    .CustomerId = customerId,
                    .FullName = fullName,
                    .Phone = phone,
                    .CreatedAt = DateTime.UtcNow
                }
            End Using
        End Function

        ' ── Create KYC Session ──────────────────────────────────────────────
        Public Function CreateSession(customerId As Integer) As String
            Dim sessionId = Guid.NewGuid().ToString()
            Dim sql As String = "INSERT INTO KycSessions (SessionId, CustomerId, Status, CreatedAt, SessionToken) " &
                      "VALUES (@sid, @cid, 'Waiting', GETUTCDATE(), @token)"
            
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
            Dim sql As String = "SELECT s.*, c.FullName As CustomerName, c.Phone As CustomerPhone " &
                      "FROM KycSessions s " &
                      "INNER JOIN Customers c ON s.CustomerId = c.CustomerId " &
                      "WHERE s.Status = 'Waiting' " &
                      "ORDER BY s.CreatedAt ASC"
                      
            Using conn As SqlConnection = DatabaseHelper.GetConnection()
                Return conn.Query(Of KycSession)(sql)
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
            Dim sql As String = "UPDATE KycSessions SET AgentId = @aid, Status = 'InProgress', UpdatedAt = GETUTCDATE() " &
                      "WHERE SessionId = @sid"
                      
            Using conn As SqlConnection = DatabaseHelper.GetConnection()
                conn.Execute(sql, New With {.aid = agentId, .sid = sessionId})
            End Using
            
            LogAudit(sessionId, "Agent Joined", "Agent ID " & agentId & " joined the call.", "Agent")
        End Sub

        ' ── Update Session Status ───────────────────────────────────────────
        Public Sub UpdateSessionStatus(sessionId As String, status As String, Optional reason As String = Nothing)
            Dim sql As String = "UPDATE KycSessions SET Status = @s, UpdatedAt = GETUTCDATE(), RejectionReason = @r " &
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
                      "VALUES (@sid, @act, @det, @by, GETUTCDATE())"
                      
            Using conn As SqlConnection = DatabaseHelper.GetConnection()
                conn.Execute(sql, New With {
                    .sid = sessionId,
                    .act = action,
                    .det = details,
                    .by = performedBy
                })
            End Using
        End Sub
    End Class
End Namespace
