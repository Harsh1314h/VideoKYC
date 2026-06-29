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

                ' Read spoken text and text score from client request
                Dim spokenText As String = context.Request.Form("spokenText")
                Dim textScoreStr As String = context.Request.Form("textScore")
                Dim textScore As Double = 0.0
                Double.TryParse(textScoreStr, textScore)

                ' Calculate final combined voice score (60% text speech-to-text matching, 40% acoustic MFCC match)
                Dim textWeight As Double = 0.6
                Dim voiceWeight As Double = 0.4
                Dim finalScore As Double = (textScore * textWeight) + (score * voiceWeight)
                Dim verified As Boolean = (finalScore >= 70.0)

                ' Log audit trail
                Dim sessionSvc As New SessionService()
                sessionSvc.LogAudit(sessionId, "Voice Recording Uploaded", "Uploaded voice clip for challenge phrase verification. Score: " & Math.Round(finalScore, 2) & "%.", "Customer")
                
                ' Save verification details in DB
                Using conn As System.Data.SqlClient.SqlConnection = Data.DatabaseHelper.GetConnection()
                    Dim sql As String = "INSERT INTO VoiceVerifications (SessionId, AudioPath, Phrase, SpokenText, TextScore, VoiceScore, FinalScore, IsVerified, CreatedAt) " &
                              "VALUES (@sid, @ap, @phrase, @st, @ts, @vs, @fs, @iv, GETDATE())"
                    conn.Execute(sql, New With {
                        .sid = sessionId,
                        .ap = "~/Uploads/" & sessionId & "/" & originalName,
                        .phrase = phrase,
                        .st = spokenText,
                        .ts = textScore,
                        .vs = score,
                        .fs = finalScore,
                        .iv = If(verified, 1, 0)
                    })
                End Using

                responseJson = JsonConvert.SerializeObject(New With {
                    .mfccScore = score,
                    .finalScore = finalScore,
                    .verified = verified
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
