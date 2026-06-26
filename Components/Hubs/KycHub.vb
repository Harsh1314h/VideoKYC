Imports System.Collections.Generic
Imports System.Linq
Imports System.Threading.Tasks
Imports Microsoft.AspNet.SignalR
Imports VideoKYC.Services

Namespace Hubs
    Public Class KycHub
        Inherits Hub

        ' In-memory session tracking. In production, this would use Redis or SQL Server State.
        Private Shared ReadOnly Sessions As New Dictionary(Of String, SessionParticipants)()
        Private Shared ReadOnly LockObj As New Object()

        Public Async Function JoinSession(sessionId As String, role As String) As Task
            ' Add the connection to the group for this session
            Await Groups.Add(Context.ConnectionId, sessionId)

            SyncLock LockObj
                If Not Sessions.ContainsKey(sessionId) Then
                    Sessions(sessionId) = New SessionParticipants()
                End If
                If role = "customer" Then
                    Sessions(sessionId).CustomerConnectionId = Context.ConnectionId
                ElseIf role = "agent" Then
                    Sessions(sessionId).AgentConnectionId = Context.ConnectionId
                End If
            End SyncLock

            If role = "agent" Then
                ' Notify the customer that the agent has joined
                Dim customerConn = Sessions(sessionId).CustomerConnectionId
                If Not String.IsNullOrEmpty(customerConn) Then
                    Await Clients.Client(customerConn).agentJoined()
                End If
                ' Notify any queue monitoring pages that this session is taken
                Await Clients.Group("agent-queue").sessionTaken(sessionId)
            End If

            If role = "customer" Then
                ' Notify the customer that the agent is already present so they can start the WebRTC offer
                Dim agentConn = Sessions(sessionId).AgentConnectionId
                If Not String.IsNullOrEmpty(agentConn) Then
                    Await Clients.Client(Context.ConnectionId).agentJoined()
                End If
            End If
        End Function

        ' ── WebRTC Signaling ────────────────────────────────────────────────

        Public Async Function SendOffer(sessionId As String, offer As String) As Task
            Dim agentConn = Sessions(sessionId).AgentConnectionId
            If Not String.IsNullOrEmpty(agentConn) Then
                Await Clients.Client(agentConn).receiveOffer(offer)
            End If
        End Function

        Public Async Function SendAnswer(sessionId As String, answer As String) As Task
            Dim customerConn = Sessions(sessionId).CustomerConnectionId
            If Not String.IsNullOrEmpty(customerConn) Then
                Await Clients.Client(customerConn).receiveAnswer(answer)
            End If
        End Function

        Public Async Function SendIceCandidate(sessionId As String, candidate As String, targetRole As String) As Task
            Dim connId = If(targetRole = "customer",
                            Sessions(sessionId).CustomerConnectionId,
                            Sessions(sessionId).AgentConnectionId)
            If Not String.IsNullOrEmpty(connId) Then
                Await Clients.Client(connId).receiveIceCandidate(candidate)
            End If
        End Function

        ' ── Verification Control Triggers (Agent ─► Customer) ──────────────────

        Public Async Function TriggerFaceVerification(sessionId As String) As Task
            Dim customerConn = Sessions(sessionId).CustomerConnectionId
            If Not String.IsNullOrEmpty(customerConn) Then
                Await Clients.Client(customerConn).startFaceCapture()
            End If
        End Function

        Public Async Function TriggerVoiceVerification(sessionId As String, phrase As String) As Task
            Dim customerConn = Sessions(sessionId).CustomerConnectionId
            If Not String.IsNullOrEmpty(customerConn) Then
                Await Clients.Client(customerConn).startVoiceCapture(phrase)
            End If
        End Function

        Public Async Function TriggerDocumentUpload(sessionId As String, docType As String) As Task
            Dim customerConn = Sessions(sessionId).CustomerConnectionId
            If Not String.IsNullOrEmpty(customerConn) Then
                Await Clients.Client(customerConn).startDocumentUpload(docType)
            End If
        End Function

        ' ── Verification Results Transfer (Customer ─► Agent) ──────────────────

        Public Async Function SendVerificationResult(sessionId As String, resultType As String, resultJson As String) As Task
            Dim agentConn = Sessions(sessionId).AgentConnectionId
            If Not String.IsNullOrEmpty(agentConn) Then
                Await Clients.Client(agentConn).receiveVerificationResult(resultType, resultJson)
            End If
        End Function

        ' ── KYC Decisions (Agent ─► Customer & DB) ───────────────────────────

        Public Async Function ApproveKyc(sessionId As String) As Task
            Await Clients.Group(sessionId).kycApproved()
            Dim svc As New SessionService()
            svc.UpdateSessionStatus(sessionId, "Approved")
        End Function

        Public Async Function RejectKyc(sessionId As String, reason As String) As Task
            Await Clients.Group(sessionId).kycRejected(reason)
            Dim svc As New SessionService()
            svc.UpdateSessionStatus(sessionId, "Rejected", reason)
        End Function

        Public Sub KeepAlive(sessionId As String)
            Dim svc As New SessionService()
            svc.KeepSessionAlive(sessionId)
        End Sub

        ' ── Disconnect Handling ─────────────────────────────────────────────

        Public Overrides Function OnDisconnected(stopCalled As Boolean) As Task
            SyncLock LockObj
                For Each kvp As KeyValuePair(Of String, SessionParticipants) In Sessions.ToList()
                    If kvp.Value.CustomerConnectionId = Context.ConnectionId Then
                        kvp.Value.CustomerConnectionId = Nothing
                        If Not String.IsNullOrEmpty(kvp.Value.AgentConnectionId) Then
                            Clients.Client(kvp.Value.AgentConnectionId).participantDisconnected()
                        End If
                        Exit For
                    ElseIf kvp.Value.AgentConnectionId = Context.ConnectionId Then
                        kvp.Value.AgentConnectionId = Nothing
                        If Not String.IsNullOrEmpty(kvp.Value.CustomerConnectionId) Then
                            Clients.Client(kvp.Value.CustomerConnectionId).participantDisconnected()
                        End If
                        Exit For
                    End If
                Next
            End SyncLock
            Return MyBase.OnDisconnected(stopCalled)
        End Function
    End Class

    Public Class SessionParticipants
        Public Property CustomerConnectionId As String
        Public Property AgentConnectionId As String
    End Class
End Namespace
