Imports System.IO
Imports System.Text.RegularExpressions
Imports System.Collections.Generic
Imports System.Linq
Imports OpenCvSharp
Imports Tesseract
Imports ZXing
Imports VideoKYC.Models

Namespace Services
    Public Class DocumentVerificationService
        
        ' Verhoeff Checksum Tables
        Private Shared ReadOnly MultiplicationTable As Integer(,) = {
            {0, 1, 2, 3, 4, 5, 6, 7, 8, 9},
            {1, 2, 3, 4, 0, 6, 7, 8, 9, 5},
            {2, 3, 4, 0, 1, 7, 8, 9, 5, 6},
            {3, 4, 0, 1, 2, 8, 9, 5, 6, 7},
            {4, 0, 1, 2, 3, 9, 5, 6, 7, 8},
            {5, 9, 8, 7, 6, 0, 4, 3, 2, 1},
            {6, 5, 9, 8, 7, 1, 0, 4, 3, 2},
            {7, 6, 5, 9, 8, 2, 1, 0, 4, 3},
            {8, 7, 6, 5, 9, 3, 2, 1, 0, 4},
            {9, 8, 7, 6, 5, 4, 3, 2, 1, 0}
        }

        Private Shared ReadOnly PermutationTable As Integer(,) = {
            {0, 1, 2, 3, 4, 5, 6, 7, 8, 9},
            {1, 5, 7, 6, 2, 8, 3, 0, 9, 4},
            {5, 8, 0, 3, 7, 9, 6, 1, 4, 2},
            {8, 9, 1, 6, 0, 4, 3, 5, 2, 7},
            {9, 4, 5, 3, 1, 2, 6, 8, 7, 0},
            {4, 2, 8, 6, 5, 7, 3, 9, 0, 1},
            {2, 7, 9, 3, 8, 0, 6, 4, 1, 5},
            {7, 0, 4, 6, 9, 1, 3, 2, 5, 8}
        }

        Private Shared ReadOnly InverseTable As Integer() = {0, 4, 3, 2, 1, 5, 6, 7, 8, 9}

        Public Function ProcessDocument(imagePath As String, docType As String) As DocumentResult
            Dim result = New DocumentResult() With {
                .DocumentType = docType,
                .ImagePath = imagePath,
                .IsVerified = False
            }

            Try
                ' Step 1: Preprocess Image
                Dim processedPath = PreprocessImage(imagePath)

                ' Step 2: OCR
                Dim rawText = RunOcr(processedPath)
                result.RawOcrText = rawText

                ' Step 3: Scan QR Code (Only for Aadhaar)
                Dim qrText = ""
                If docType.ToLower() = "aadhaar" Then
                    qrText = ScanQrCode(imagePath)
                    If Not String.IsNullOrEmpty(qrText) Then
                        result.Fields("QR Code Data") = qrText
                    End If
                End If

                ' Step 4: Extract Specific Fields
                Select Case docType.ToLower()
                    Case "aadhaar"
                        ExtractAadhaar(result, rawText)
                    Case "pan"
                        ExtractPan(result, rawText)
                    Case "passport"
                        ExtractPassport(result, rawText)
                    Case "dl"
                        ExtractDrivingLicence(result, rawText)
                End Select

            Catch ex As Exception
                result.Fields("Error") = "Processing failed: " & ex.Message
            End Try

            Return result
        End Function

        ' ── Image Preprocessing ─────────────────────────────────────────────────
        Private Function PreprocessImage(inputPath As String) As String
            Dim dir = Path.GetDirectoryName(inputPath)
            Dim name = Path.GetFileNameWithoutExtension(inputPath)
            Dim ext = Path.GetExtension(inputPath)
            Dim outputPath = Path.Combine(dir, name & "_processed" & ext)

            Using img = Cv2.ImRead(inputPath)
                Using gray = New Mat()
                Using denoised = New Mat()
                Using binary = New Mat()
                    ' Convert to Grayscale
                    Cv2.CvtColor(img, gray, ColorConversionCodes.BGR2GRAY)
                    
                    ' Denoise image
                    Cv2.FastNlMeansDenoising(gray, denoised, 10, 7, 21)
                    
                    ' Adaptive Threshold (Binarization)
                    Cv2.AdaptiveThreshold(denoised, binary, 255,
                                           AdaptiveThresholdTypes.GaussianC,
                                           ThresholdTypes.Binary, 11, 2)
                    
                    binary.SaveImage(outputPath)
                End Using
                End Using
                End Using
            End Using

            Return outputPath
        End Function

        ' ── OCR text extraction ──────────────────────────────────────────────────
        Private Function RunOcr(imagePath As String) As String
            Dim tessDataPath = HttpContext.Current.Server.MapPath("~/App_Data/tessdata")
            Using engine = New TesseractEngine(tessDataPath, "eng+hin", EngineMode.Default)
                Using img = Pix.LoadFromFile(imagePath)
                    Using page = engine.Process(img)
                        Return page.GetText()
                    End Using
                End Using
            End Using
        End Function

        ' ── Scan QR Code ────────────────────────────────────────────────────────
        Private Function ScanQrCode(imagePath As String) As String
            Try
                ' Load as bitmap
                Using bmp = New System.Drawing.Bitmap(imagePath)
                    Dim reader = New BarcodeReader()
                    Dim result = reader.Decode(bmp)
                    If result IsNot Nothing Then
                        Return result.Text
                    End If
                End Using
            Catch
                ' Return empty if no barcode detected
            End Try
            Return String.Empty
        End Function

        ' ── Aadhaar Card Parsing ─────────────────────────────────────────────────
        Private Sub ExtractAadhaar(result As DocumentResult, text As String)
            ' Aadhaar Number Regex (12 digits with optional spaces)
            Dim uidMatch = Regex.Match(text, "\b(\d{4}\s?\d{4}\s?\d{4})\b")
            If uidMatch.Success Then
                Dim uid = uidMatch.Value.Replace(" ", "")
                result.DocumentNumber = uid
                result.Fields("Aadhaar Number") = uidMatch.Value
                result.IsVerified = ValidateVerhoeff(uid)
            Else
                result.Fields("Aadhaar Number") = "Not Found"
            End If

            ' Date of Birth
            Dim dobMatch = Regex.Match(text, "(?:DOB|Date of Birth|जन्म तिथि)[:\s]+(\d{2}[/-]\d{2}[/-]\d{4}|\d{4})")
            If dobMatch.Success Then
                result.Fields("Date of Birth") = dobMatch.Groups(1).Value
            End If

            ' Gender
            If text.ToUpper().Contains("FEMALE") OrElse text.Contains("महिला") Then
                result.Fields("Gender") = "Female"
            ElseIf text.ToUpper().Contains("MALE") OrElse text.Contains("पुरुष") Then
                result.Fields("Gender") = "Male"
            End If
        End Sub

        ' ── PAN Card Parsing ─────────────────────────────────────────────────────
        Private Sub ExtractPan(result As DocumentResult, text As String)
            ' PAN Format: 5 Alphabets, 4 Digits, 1 Alphabet
            Dim panMatch = Regex.Match(text, "\b([A-Z]{5}\d{4}[A-Z])\b")
            If panMatch.Success Then
                result.DocumentNumber = panMatch.Value
                result.Fields("PAN Number") = panMatch.Value
                result.IsVerified = True ' Correct format found
            Else
                result.Fields("PAN Number") = "Not Found"
            End If

            ' DOB
            Dim dobMatch = Regex.Match(text, "\b(\d{2}[/-]\d{2}[/-]\d{4})\b")
            If dobMatch.Success Then
                result.Fields("Date of Birth") = dobMatch.Value
            End If

            ' Extract Names using line splitting
            Dim lines = text.Split(New String() {vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries)
            Dim lineIndex = 0
            For Each line As String In lines
                If line.ToUpper().Contains("NAME") OrElse line.Contains("नाम") Then
                    ' Usually the name is on the next line or on the same line after token
                    If lineIndex + 1 < lines.Length Then
                        result.Fields("Name") = lines(lineIndex + 1).Trim()
                    End If
                End If
                If line.ToUpper().Contains("FATHER") OrElse line.Contains("पिता") Then
                    If lineIndex + 1 < lines.Length Then
                        result.Fields("Father's Name") = lines(lineIndex + 1).Trim()
                    End If
                End If
                lineIndex += 1
            Next
        End Sub

        ' ── Passport Parsing ────────────────────────────────────────────────────
        Private Sub ExtractPassport(result As DocumentResult, text As String)
            ' Passport Number format: letter followed by 7 digits
            Dim passportMatch = Regex.Match(text, "\b([A-Z][0-9]{7})\b")
            If passportMatch.Success Then
                result.DocumentNumber = passportMatch.Value
                result.Fields("Passport Number") = passportMatch.Value
                result.IsVerified = True
            Else
                result.Fields("Passport Number") = "Not Found"
            End If

            ' MRZ Parsing (usually at the bottom)
            Dim lines = text.Split(New String() {vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries)
            For Each line As String In lines
                Dim cleanLine = line.Replace(" ", "").Trim()
                ' Line 1 of MRZ: P<IND...
                If cleanLine.StartsWith("P<IND") Then
                    Dim namesSection = cleanLine.Substring(5)
                    Dim parts = namesSection.Split(New String() {"<<"}, StringSplitOptions.RemoveEmptyEntries)
                    If parts.Length > 0 Then result.Fields("Surname") = parts(0).Replace("<", " ")
                    If parts.Length > 1 Then result.Fields("Given Name") = parts(1).Replace("<", " ")
                End If
                
                ' Line 2 of MRZ: PassportNo (9 chars) + check digit + Nationality (3 chars) + DOB (6 chars: YYMMDD) + check digit + Gender (M/F) + Expiry (6 chars) + ...
                If cleanLine.Length >= 28 AndAlso Regex.IsMatch(cleanLine.Substring(0, 9), "^[A-Z0-9]{9}$") AndAlso Regex.IsMatch(cleanLine.Substring(10, 3), "^[A-Z]{3}$") Then
                    ' Date of birth (YYMMDD)
                    Dim dobStr = cleanLine.Substring(13, 6)
                    Dim genderStr = cleanLine.Substring(20, 1)
                    Dim expStr = cleanLine.Substring(21, 6)
                    
                    result.Fields("MRZ DOB") = dobStr.Substring(4, 2) & "/" & dobStr.Substring(2, 2) & "/19" & dobStr.Substring(0, 2)
                    result.Fields("MRZ Gender") = If(genderStr = "M", "Male", "Female")
                    result.Fields("MRZ Expiry") = expStr.Substring(4, 2) & "/" & expStr.Substring(2, 2) & "/20" & expStr.Substring(0, 2)
                End If
            Next
        End Sub

        ' ── Driving Licence Parsing ──────────────────────────────────────────────
        Private Sub ExtractDrivingLicence(result As DocumentResult, text As String)
            ' Indian DL Number: e.g. MH0420110001234 or DL-1320140001234
            Dim dlMatch = Regex.Match(text, "\b([A-Z]{2}[0-9]{2}[A-Z0-9]{11})\b|\b([A-Z]{2}[- ]?[0-9]{2}[- ]?[0-9]{4}[- ]?[0-9]{7})\b")
            If dlMatch.Success Then
                Dim rawDl = dlMatch.Value.Replace("-", "").Replace(" ", "")
                result.DocumentNumber = rawDl
                result.Fields("DL Number") = dlMatch.Value
                result.IsVerified = True
            Else
                result.Fields("DL Number") = "Not Found"
            End If

            ' Date of Birth
            Dim dobMatch = Regex.Match(text, "(?:DOB|Date of Birth|जन्म तिथि|Birth Date)[:\s]+(\d{2}[/-]\d{2}[/-]\d{4})")
            If dobMatch.Success Then
                result.Fields("Date of Birth") = dobMatch.Groups(1).Value
            End If

            ' Valid Up To
            Dim valMatch = Regex.Match(text, "(?:VALID|EXPIRY|NT|Till|Upto)[:\s]+(\d{2}[/-]\d{2}[/-]\d{4})", RegexOptions.IgnoreCase)
            If valMatch.Success Then
                result.Fields("Valid Up To") = valMatch.Groups(1).Value
            End If
        End Sub

        ' ── Verhoeff Checksum Logic ─────────────────────────────────────────────
        Public Function ValidateVerhoeff(num As String) As Boolean
            Try
                Dim c As Integer = 0
                Dim myArray As Integer() = num.Select(Function(ch) Integer.Parse(ch.ToString())).ToArray()
                
                For i As Integer = 0 To myArray.Length - 1
                    Dim index As Integer = myArray.Length - 1 - i
                    c = MultiplicationTable(c, PermutationTable(i Mod 8, myArray(index)))
                Next
                
                Return c = 0
            Catch
                Return False
            End Try
        End Function
    End Class
End Namespace
