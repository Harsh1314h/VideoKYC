Imports System.IO
Imports System.Web
Imports Newtonsoft.Json
Imports Dapper
Imports VideoKYC.Services

Namespace Handlers
    Public Class UploadDocument
        Implements IHttpHandler

        Public Sub ProcessRequest(context As HttpContext) Implements IHttpHandler.ProcessRequest
            Dim responseJson As String = ""
            Try
                ' Validate inputs
                Dim sessionId = context.Request.Form("sessionId")
                Dim docType = context.Request.Form("docType")
                
                If context.Request.Files.Count = 0 Then
                    Throw New Exception("No document image was uploaded.")
                End If
                Dim file = context.Request.Files(0)

                If String.IsNullOrEmpty(sessionId) OrElse String.IsNullOrEmpty(docType) Then
                    Throw New Exception("Session ID or Document Type is missing.")
                End If

                ' Establish session directories
                Dim uploadsDir = context.Server.MapPath("~/Uploads/" & sessionId)
                If Not Directory.Exists(uploadsDir) Then
                    Directory.CreateDirectory(uploadsDir)
                End If

                ' Save the image file as raw doc
                Dim fileExt = Path.GetExtension(file.FileName)
                If String.IsNullOrEmpty(fileExt) Then fileExt = ".jpg"
                
                Dim originalName = "doc_" & docType.ToLower() & fileExt
                Dim originalPath = Path.Combine(uploadsDir, originalName)
                file.SaveAs(originalPath)

                ' Run processing pipeline
                Dim docSvc As New DocumentVerificationService()
                Dim result = docSvc.ProcessDocument(originalPath, docType)

                ' Save the document photo image if OCR verified
                If result.IsVerified AndAlso File.Exists(originalPath) Then
                    ' Copy as standard reference for face-matching
                    Dim refFacePath = Path.Combine(uploadsDir, "doc_face.jpg")
                    ' For demo purposes, we will treat the document photo itself as standard
                    File.Copy(originalPath, refFacePath, True)
                End If

                ' Log audit trail
                Dim sessionSvc As New SessionService()
                sessionSvc.LogAudit(sessionId, "Document Uploaded", 
                                    "Uploaded document " & docType & ". Number: " & result.DocumentNumber & ". Checksum Match: " & result.IsVerified & ".", 
                                    "Customer")

                ' Save verification details in DB
                Using conn As System.Data.SqlClient.SqlConnection = Data.DatabaseHelper.GetConnection()
                    Dim sql As String = "INSERT INTO DocumentVerifications (SessionId, DocumentType, DocumentNumber, IsVerified, ExtractedDataJson, ImagePath, OcrText, CreatedAt) " &
                              "VALUES (@sid, @dt, @dn, @iv, @json, @ip, @ocr, GETUTCDATE())"
                    conn.Execute(sql, New With {
                        .sid = sessionId,
                        .dt = docType,
                        .dn = result.DocumentNumber,
                        .iv = If(result.IsVerified, 1, 0),
                        .json = JsonConvert.SerializeObject(result.Fields),
                        .ip = "~/Uploads/" & sessionId & "/" & originalName,
                        .ocr = result.RawOcrText
                    })
                End Using

                responseJson = JsonConvert.SerializeObject(result)

            Catch ex As Exception
                responseJson = JsonConvert.SerializeObject(New With {
                    .DocumentType = "Unknown",
                    .DocumentNumber = "",
                    .IsVerified = False,
                    .RawOcrText = "",
                    .Fields = New Dictionary(Of String, String)() From {{"Error", ex.Message}},
                    .ImagePath = ""
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
