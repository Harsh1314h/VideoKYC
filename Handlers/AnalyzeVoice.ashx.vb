Imports System.IO
Imports System.Web
Imports Newtonsoft.Json
Imports Dapper
Imports VideoKYC.Services

Namespace Handlers
    Public Class AnalyzeVoice
        Implements IHttpHandler

        Public Sub ProcessRequest(context As HttpContext) Implements IHttpHandler.ProcessRequest
            Dim responseJson As String = ""
            Try
                Dim sessionId = context.Request.Form("sessionId")
                Dim phrase = context.Request.Form("phrase")

                If context.Request.Files.Count = 0 Then
                    Throw New Exception("No voice clip was uploaded.")
                End If
                Dim file = context.Request.Files(0)

                If String.IsNullOrEmpty(sessionId) Then
                    Throw New Exception("Session ID is missing.")
                End If

                ' Save file to session directory
                Dim uploadsDir = context.Server.MapPath("~/Uploads/" & sessionId)
                If Not Directory.Exists(uploadsDir) Then
                    Directory.CreateDirectory(uploadsDir)
                End If

                Dim originalName = "voice.webm"
                Dim originalPath = Path.Combine(uploadsDir, originalName)
                file.SaveAs(originalPath)

                ' Run MFCC energy analysis
                Dim voiceSvc As New VoiceVerificationService()
                Dim score = voiceSvc.AnalyzeVoice(originalPath)

                ' Log audit trail
                Dim sessionSvc As New SessionService()
                sessionSvc.LogAudit(sessionId, "Voice Recording Uploaded", "Uploaded voice clip for challenge phrase verification. Score: " & score & "%.", "Customer")
                ' Save verification details in DB
                Using conn As System.Data.SqlClient.SqlConnection = Data.DatabaseHelper.GetConnection()
                    Dim sql As String = "INSERT INTO VoiceVerifications (SessionId, AudioPath, Phrase, TextScore, VoiceScore, FinalScore, IsVerified, CreatedAt) " &
                              "VALUES (@sid, @ap, @phrase, @ts, @vs, @fs, @iv, GETDATE())"
                    conn.Execute(sql, New With {
                        .sid = sessionId,
                        .ap = "~/Uploads/" & sessionId & "/" & originalName,
                        .phrase = phrase,
                        .ts = 100.0,
                        .vs = score,
                        .fs = score,
                        .iv = If(score >= 70.0, 1, 0)
                    })
                End Using

                responseJson = JsonConvert.SerializeObject(New With {
                    .mfccScore = score
                })

            Catch ex As Exception
                responseJson = JsonConvert.SerializeObject(New With {
                    .mfccScore = 50.0,
                    .errorMsg = ex.Message
                })
            End Try

            context.Response.ContentType = "application/json"
            context.Response.Write(responseJson)
        End Sub

        Public ReadOnly Property IsReusable As Boolean Implements IHttpHandler.IsReusable
            Get
                Return False
            End Get
        End Property
    End Class
End Namespace
