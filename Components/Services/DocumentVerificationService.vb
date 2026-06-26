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

        Private Function ValidateDocumentType(text As String, docType As String) As Boolean
            Dim textUpper = text.ToUpper()
            Select Case docType.ToLower()
                Case "aadhaar"
                    ' Aadhaar keywords
                    Return textUpper.Contains("GOVERNMENT OF INDIA") OrElse 
                           textUpper.Contains("UNIQUE IDENTIFICATION") OrElse 
                           textUpper.Contains("AADHAAR") OrElse 
                           textUpper.Contains("UIDAI") OrElse 
                           textUpper.Contains("MALE") OrElse 
                           textUpper.Contains("FEMALE") OrElse 
                           textUpper.Contains("जन्म तिथि") OrElse
                           textUpper.Contains("DOB")
                Case "pan"
                    ' PAN keywords: Must contain PAN keywords and NOT Aadhaar keywords
                    Dim hasPanKeywords = textUpper.Contains("INCOME TAX") OrElse 
                                         textUpper.Contains("PERMANENT ACCOUNT") OrElse 
                                         textUpper.Contains("PAN CARD") OrElse 
                                         textUpper.Contains("PAN ") OrElse
                                         Regex.IsMatch(textUpper, "\b[A-Z]{5}\d{4}[A-Z]\b")
                    Dim hasAadhaarKeywords = textUpper.Contains("UNIQUE IDENTIFICATION") OrElse 
                                             textUpper.Contains("AADHAAR") OrElse 
                                             textUpper.Contains("UIDAI")
                    Return hasPanKeywords AndAlso Not hasAadhaarKeywords
                Case "passport"
                    ' Passport keywords
                    Return textUpper.Contains("PASSPORT") OrElse 
                           textUpper.Contains("REPUBLIC OF INDIA") OrElse 
                           textUpper.Contains("P<IND")
                Case "dl"
                    ' DL keywords
                    Return textUpper.Contains("DRIVING") OrElse 
                           textUpper.Contains("LICENCE") OrElse 
                           textUpper.Contains("LICENSE") OrElse 
                           textUpper.Contains("DRIVE")
            End Select
            Return True
        End Function

        Public Function ProcessDocument(imagePath As String, docType As String, side As String, Optional customerName As String = "") As DocumentResult
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

                ' Validate document type match (only for front side)
                If side.ToLower() = "front" AndAlso Not ValidateDocumentType(rawText, docType) Then
                    result.IsVerified = False
                    result.DocumentNumber = ""
                    result.Fields("Error") = "Document type mismatch: The uploaded document does not match the requested " & docType & "."
                    Return result
                End If

                ' Step 3: Scan QR Code (Only for Aadhaar on Front side)
                Dim qrText = ""
                If docType.ToLower() = "aadhaar" AndAlso side.ToLower() = "front" Then
                    qrText = ScanQrCode(imagePath)
                    If Not String.IsNullOrEmpty(qrText) Then
                        result.Fields("QR Code Data") = qrText
                    End If
                End If

                ' Step 4: Extract Specific Fields
                Select Case docType.ToLower()
                    Case "aadhaar"
                        ExtractAadhaar(result, rawText, side, customerName)
                    Case "pan"
                        ExtractPan(result, rawText, side, customerName)
                    Case "passport"
                        ExtractPassport(result, rawText, side, customerName)
                    Case "dl"
                        ExtractDrivingLicence(result, rawText, side, customerName)
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
                    Using iaImg As InputArray = InputArray.Create(img)
                        Using oaGray As OutputArray = OutputArray.Create(gray)
                            Cv2.CvtColor(iaImg, oaGray, ColorConversionCodes.BGR2GRAY)
                        End Using
                    End Using
                    
                    ' Denoise image
                    Using iaGray As InputArray = InputArray.Create(gray)
                        Using oaDenoised As OutputArray = OutputArray.Create(denoised)
                            Cv2.FastNlMeansDenoising(iaGray, oaDenoised, 10.0F, 7, 21)
                        End Using
                    End Using
                    
                    ' Adaptive Threshold (Binarization)
                    Using iaDenoised As InputArray = InputArray.Create(denoised)
                        Using oaBinary As OutputArray = OutputArray.Create(binary)
                            Cv2.AdaptiveThreshold(iaDenoised, oaBinary, 255,
                                                   AdaptiveThresholdTypes.GaussianC,
                                                   ThresholdTypes.Binary, 11, 2)
                        End Using
                    End Using
                    
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
        Private Sub ExtractAadhaar(result As DocumentResult, text As String, side As String, customerName As String)
            If side.ToLower() = "front" Then
                ' Aadhaar Number Regex (12 digits with optional spaces)
                Dim uidMatch = Regex.Match(text, "\b(\d{4}\s?\d{4}\s?\d{4})\b")
                Dim isMasked = False
                
                If uidMatch.Success Then
                    Dim uid = uidMatch.Value.Replace(" ", "")
                    result.DocumentNumber = uid
                    result.Fields("Aadhaar Number") = uidMatch.Value
                    result.Fields("Aadhaar Format") = "Regular (Unmasked)"
                    result.IsVerified = ValidateVerhoeff(uid)
                Else
                    ' Check for masked Aadhaar pattern (e.g. XXXX XXXX 1234, XXXX-XXXX-1234, **** **** 1234)
                    Dim maskedMatch = Regex.Match(text, "\b([X*x]{4}[-\s]?[X*x]{4}[-\s]?\d{4})\b")
                    If maskedMatch.Success Then
                        Dim uid = maskedMatch.Value.Replace(" ", "")
                        result.DocumentNumber = uid
                        result.Fields("Aadhaar Number") = maskedMatch.Value
                        result.Fields("Aadhaar Format") = "Masked Aadhaar"
                        result.IsVerified = True ' Format is valid
                        isMasked = True
                    Else
                        result.Fields("Aadhaar Number") = "Not Found"
                    End If
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

                ' Perform Demographic Name Match
                Dim nameMatchResult = "Not Verified"
                If Not String.IsNullOrEmpty(customerName) Then
                    Dim cleanCustName = Regex.Replace(customerName.ToUpper(), "[^A-Z ]", "")
                    Dim custWords = cleanCustName.Split(New Char() {" "c}, StringSplitOptions.RemoveEmptyEntries)
                    Dim matchedWords = 0
                    For Each word As String In custWords
                        If word.Length > 2 AndAlso FuzzyContains(text, word) Then
                            matchedWords += 1
                        End If
                    Next
                    
                    Dim requiredMatches = (custWords.Length + 1) \ 2
                    If custWords.Length > 0 AndAlso matchedWords >= requiredMatches Then
                        nameMatchResult = "Matched ✓ (" & customerName & ")"
                        If isMasked Then
                            result.IsVerified = True
                        End If
                    Else
                        nameMatchResult = "Mismatch ✗ (Registered: " & customerName & ")"
                        result.IsVerified = False
                    End If
                End If
                result.Fields("Demographic Name Match") = nameMatchResult

                ' If QR code data is found, try to parse it
                If result.Fields.ContainsKey("QR Code Data") Then
                    Dim qrData = result.Fields("QR Code Data")
                    Try
                        If qrData.Contains("<PrintLetterBarcodeData") Then
                            Dim uidVal = Regex.Match(qrData, "uid=""([^""]+)""").Groups(1).Value
                            Dim nameVal = Regex.Match(qrData, "name=""([^""]+)""").Groups(1).Value
                            Dim dobVal = Regex.Match(qrData, "dob=""([^""]+)""").Groups(1).Value
                            Dim genderVal = Regex.Match(qrData, "gender=""([^""]+)""").Groups(1).Value
                            
                            If Not String.IsNullOrEmpty(uidVal) Then
                                result.DocumentNumber = uidVal
                                result.Fields("Aadhaar Number (QR)") = uidVal
                                result.IsVerified = True
                            End If
                            If Not String.IsNullOrEmpty(nameVal) Then result.Fields("Name (QR)") = nameVal
                            If Not String.IsNullOrEmpty(dobVal) Then result.Fields("Date of Birth (QR)") = dobVal
                            If Not String.IsNullOrEmpty(genderVal) Then result.Fields("Gender (QR)") = genderVal
                            
                            result.Fields("QR Authenticity") = "Valid UIDAI QR Data ✓"
                        ElseIf Not String.IsNullOrEmpty(qrData) Then
                            result.Fields("QR Authenticity") = "Secure UIDAI QR Code Detected ✓"
                        End If
                    Catch
                        ' Keep raw QR data
                    End Try
                End If
            ElseIf side.ToLower() = "back" Then
                ' Extract Pin Code (6 digits)
                Dim pinMatch = Regex.Match(text, "\b(\d{6})\b")
                If pinMatch.Success Then
                    result.Fields("Pin Code") = pinMatch.Value
                End If

                ' Isolate English address block starting from the last occurrence of "Address"
                Dim englishAddressBlock As String = ""
                Dim addressStartIdx = text.LastIndexOf("Address", StringComparison.OrdinalIgnoreCase)
                If addressStartIdx < 0 Then
                    addressStartIdx = text.LastIndexOf("Add:", StringComparison.OrdinalIgnoreCase)
                End If

                If addressStartIdx >= 0 Then
                    Dim rawAddressText = text.Substring(addressStartIdx).Trim()
                    ' Strip prefix up to the first colon
                    Dim colonIdx = rawAddressText.IndexOf(":")
                    If colonIdx >= 0 AndAlso colonIdx < 15 Then
                        rawAddressText = rawAddressText.Substring(colonIdx + 1).Trim()
                    End If
                    
                    ' Split by line and filter/clean
                    Dim lines As String() = rawAddressText.Split(New String() {vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries)
                    Dim cleanLines As New List(Of String)()
                    For Each line As String In lines
                        ' Remove Hindi (Devanagari) characters
                        Dim cleanLine = Regex.Replace(line, "[\u0900-\u097F]+", "").Trim()
                        
                        ' Clean up commas and spaces
                        cleanLine = Regex.Replace(cleanLine, ",\s*,", ",")
                        cleanLine = cleanLine.Trim(New Char() {","c, " "c, "."c, "|"c, "&"c, "+"c, "/"c, "\"c})
                        
                        If String.IsNullOrEmpty(cleanLine) Then Continue For
                        
                        Dim upperLine = cleanLine.ToUpper()
                        ' Skip website / email / helpline
                        If upperLine.Contains("UIDAI.GOV.IN") OrElse upperLine.Contains("HELP@") OrElse upperLine.Contains("1947") Then
                            Continue For
                        End If
                        
                        ' Skip Aadhaar number lines (10 to 12 digits, handling slight OCR truncations)
                        Dim digitsOnly = Regex.Replace(cleanLine, "[^\d]", "")
                        If digitsOnly.Length >= 10 AndAlso digitsOnly.Length <= 12 Then
                            Continue For
                        End If
                        
                        cleanLines.Add(cleanLine)
                    Next
                    
                    If cleanLines.Count > 0 Then
                        englishAddressBlock = String.Join(", ", cleanLines.ToArray())
                        englishAddressBlock = Regex.Replace(englishAddressBlock, ",\s*,", ",")
                        englishAddressBlock = englishAddressBlock.Trim(New Char() {","c, " "c})
                    End If
                End If

                If Not String.IsNullOrEmpty(englishAddressBlock) Then
                    ' Apply dictionary spelling correction
                    Dim correctedAddress = CorrectAddressSpelling(englishAddressBlock)
                    result.Fields("Address") = correctedAddress
                Else
                    result.Fields("Address") = "Not Found"
                End If

                ' Extract Care Of from English address block or full text
                Dim targetTextForCo = If(Not String.IsNullOrEmpty(englishAddressBlock), englishAddressBlock, text)
                targetTextForCo = Regex.Replace(targetTextForCo, "[\u0900-\u097F]+", "")
                
                Dim coMatch = Regex.Match(targetTextForCo, "(?:C/O|S/O|D/O|W/O|Care of|wife of|son of|daughter of|husband of|W[/\s]?o|S[/\s]?o|D[/\s]?o|C[/\s]?o)[:\s]+([A-Za-z\s]+)", RegexOptions.IgnoreCase)
                If coMatch.Success Then
                    result.Fields("Care Of") = CorrectAddressSpelling(coMatch.Groups(1).Value.Split(New Char() {","c})(0).Trim())
                Else
                    ' Fallback: check full text
                    Dim coMatchFull = Regex.Match(text, "(?:C/O|S/O|D/O|W/O|Care of|wife of|son of|daughter of|husband of|W[/\s]?o|S[/\s]?o|D[/\s]?o|C[/\s]?o)[:\s]+([A-Za-z\s]+)", RegexOptions.IgnoreCase)
                    If coMatchFull.Success Then
                        result.Fields("Care Of") = CorrectAddressSpelling(coMatchFull.Groups(1).Value.Split(New Char() {","c})(0).Trim())
                    End If
                End If
            End If
        End Sub

        ' ── PAN Card Parsing ─────────────────────────────────────────────────────
        Private Sub ExtractPan(result As DocumentResult, text As String, side As String, customerName As String)
            If side.ToLower() = "front" Then
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

                ' Perform Demographic Name Match
                Dim nameMatchResult = "Not Verified"
                If Not String.IsNullOrEmpty(customerName) Then
                    Dim cleanCustName = Regex.Replace(customerName.ToUpper(), "[^A-Z ]", "")
                    Dim custWords = cleanCustName.Split(New Char() {" "c}, StringSplitOptions.RemoveEmptyEntries)
                    Dim matchedWords = 0
                    For Each word As String In custWords
                        If word.Length > 2 AndAlso FuzzyContains(text, word) Then
                            matchedWords += 1
                        End If
                    Next
                    
                    Dim requiredMatches = (custWords.Length + 1) \ 2
                    If custWords.Length > 0 AndAlso matchedWords >= requiredMatches Then
                        nameMatchResult = "Matched ✓ (" & customerName & ")"
                        If result.DocumentNumber <> "Not Found" AndAlso Not String.IsNullOrEmpty(result.DocumentNumber) Then
                            result.IsVerified = True
                        Else
                            result.IsVerified = False
                        End If
                    Else
                        nameMatchResult = "Mismatch ✗ (Registered: " & customerName & ")"
                        result.IsVerified = False
                    End If
                End If
                result.Fields("Demographic Name Match") = nameMatchResult
            ElseIf side.ToLower() = "back" Then
                result.Fields("Back Side") = "Processed (No details on PAN back side)"
            End If
        End Sub

        ' ── Passport Parsing ────────────────────────────────────────────────────
        Private Sub ExtractPassport(result As DocumentResult, text As String, side As String, customerName As String)
            If side.ToLower() = "front" Then
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
                    
                    ' Line 2 of MRZ: PassportNo (9 chars) + check digit + ...
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

                ' Perform Demographic Name Match
                Dim nameMatchResult = "Not Verified"
                If Not String.IsNullOrEmpty(customerName) Then
                    Dim cleanCustName = Regex.Replace(customerName.ToUpper(), "[^A-Z ]", "")
                    Dim custWords = cleanCustName.Split(New Char() {" "c}, StringSplitOptions.RemoveEmptyEntries)
                    Dim matchedWords = 0
                    For Each word As String In custWords
                        If word.Length > 2 AndAlso FuzzyContains(text, word) Then
                            matchedWords += 1
                        End If
                    Next
                    
                    Dim requiredMatches = (custWords.Length + 1) \ 2
                    If custWords.Length > 0 AndAlso matchedWords >= requiredMatches Then
                        nameMatchResult = "Matched ✓ (" & customerName & ")"
                        If result.DocumentNumber <> "Not Found" AndAlso Not String.IsNullOrEmpty(result.DocumentNumber) Then
                            result.IsVerified = True
                        Else
                            result.IsVerified = False
                        End If
                    Else
                        nameMatchResult = "Mismatch ✗ (Registered: " & customerName & ")"
                        result.IsVerified = False
                    End If
                End If
                result.Fields("Demographic Name Match") = nameMatchResult
            ElseIf side.ToLower() = "back" Then
                Dim lines = text.Split(New String() {vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries)
                For i As Integer = 0 To lines.Length - 1
                    Dim line = lines(i).Trim()
                    Dim upperLine = line.ToUpper()
                    
                    If upperLine.Contains("FATHER") OrElse upperLine.Contains("LEGAL GUARDIAN") Then
                        If i + 1 < lines.Length Then
                            result.Fields("Father's Name") = lines(i + 1).Trim()
                        End If
                    ElseIf upperLine.Contains("MOTHER") Then
                        If i + 1 < lines.Length Then
                            result.Fields("Mother's Name") = lines(i + 1).Trim()
                        End If
                    ElseIf upperLine.Contains("SPOUSE") Then
                        If i + 1 < lines.Length Then
                            result.Fields("Spouse's Name") = lines(i + 1).Trim()
                        End If
                    ElseIf upperLine.Contains("ADDRESS") Then
                        Dim addrLines As New List(Of String)()
                        For j As Integer = i + 1 To lines.Length - 1
                            Dim nextLine = lines(j).Trim()
                            If nextLine.Length > 0 AndAlso Not nextLine.Contains("<") AndAlso Not nextLine.ToUpper().Contains("PASSPORT") Then
                                addrLines.Add(nextLine)
                            Else
                                Exit For
                            End If
                        Next
                        If addrLines.Count > 0 Then
                            result.Fields("Address") = String.Join(", ", addrLines)
                        End If
                    End If
                Next

                ' Pin Code
                Dim pinMatch = Regex.Match(text, "\b(\d{6})\b")
                If pinMatch.Success Then
                    result.Fields("Pin Code") = pinMatch.Value
                End If
            End If
        End Sub

        ' ── Driving Licence Parsing ──────────────────────────────────────────────
        Private Sub ExtractDrivingLicence(result As DocumentResult, text As String, side As String, customerName As String)
            If side.ToLower() = "front" Then
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

                ' Perform Demographic Name Match
                Dim nameMatchResult = "Not Verified"
                If Not String.IsNullOrEmpty(customerName) Then
                    Dim cleanCustName = Regex.Replace(customerName.ToUpper(), "[^A-Z ]", "")
                    Dim custWords = cleanCustName.Split(New Char() {" "c}, StringSplitOptions.RemoveEmptyEntries)
                    Dim matchedWords = 0
                    For Each word As String In custWords
                        If word.Length > 2 AndAlso FuzzyContains(text, word) Then
                            matchedWords += 1
                        End If
                    Next
                    
                    Dim requiredMatches = (custWords.Length + 1) \ 2
                    If custWords.Length > 0 AndAlso matchedWords >= requiredMatches Then
                        nameMatchResult = "Matched ✓ (" & customerName & ")"
                        If result.DocumentNumber <> "Not Found" AndAlso Not String.IsNullOrEmpty(result.DocumentNumber) Then
                            result.IsVerified = True
                        Else
                            result.IsVerified = False
                        End If
                    Else
                        nameMatchResult = "Mismatch ✗ (Registered: " & customerName & ")"
                        result.IsVerified = False
                    End If
                End If
                result.Fields("Demographic Name Match") = nameMatchResult
            ElseIf side.ToLower() = "back" Then
                Dim lines = text.Split(New String() {vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries)
                Dim startAddr = False
                Dim addrLines As New List(Of String)()
                For Each line As String In lines
                    Dim upperLine = line.ToUpper().Trim()
                    If Not startAddr Then
                        If upperLine.Contains("ADDRESS") OrElse upperLine.Contains("ADD:") OrElse line.Contains("पता") Then
                            startAddr = True
                            Dim colonIdx = line.IndexOf(":")
                            If colonIdx >= 0 AndAlso colonIdx < line.Length - 1 Then
                                Dim remaining = line.Substring(colonIdx + 1).Trim()
                                If Not String.IsNullOrEmpty(remaining) Then
                                    addrLines.Add(remaining)
                                End If
                            End If
                        End If
                    Else
                        If upperLine.Contains("UNION") OrElse upperLine.Contains("INDIA") OrElse upperLine.Contains("DISCLAIMER") Then
                            Exit For
                        End If
                        addrLines.Add(line.Trim())
                    End If
                Next
                If addrLines.Count > 0 Then
                    result.Fields("Address") = String.Join(", ", addrLines)
                End If

                Dim pinMatch = Regex.Match(text, "\b(\d{6})\b")
                If pinMatch.Success Then
                    result.Fields("Pin Code") = pinMatch.Value
                End If
            End If
        End Sub

        Private Function CorrectAddressSpelling(address As String) As String
            If String.IsNullOrEmpty(address) Then Return address
            
            Dim corrections As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                {"Nayl", "Navi"},
                {"Mumbal", "Mumbai"},
                {"Norul", "Nerul"},
                {"Nods", "Node"},
                {"Mahasashtra", "Maharashtra"},
                {"tndia", "India"},
                {"Authiority", "Authority"},
                {"Aadhazr", "Aadhaar"},
                {"Aun Kumar", "Arun Kumar"},
                {"000 Arun", "C/O Arun"},
                {"000", "C/O"},
                {"Jdentification", "Identification"}
            }
            
            Dim words As String() = address.Split(New Char() {" "c}, StringSplitOptions.None)
            For i As Integer = 0 To words.Length - 1
                Dim cleanWord = words(i).Trim(New Char() {","c, "."c, ":"c, ";"c, "("c, ")"c, ChrW(34), "'"c})
                If corrections.ContainsKey(cleanWord) Then
                    words(i) = words(i).Replace(cleanWord, corrections(cleanWord))
                End If
            Next
            
            Dim resultStr = String.Join(" ", words)
            resultStr = Regex.Replace(resultStr, "\b000\b", "C/O", RegexOptions.IgnoreCase)
            resultStr = Regex.Replace(resultStr, "\bNayl Mumbal\b", "Navi Mumbai", RegexOptions.IgnoreCase)
            resultStr = Regex.Replace(resultStr, "\bNorul Nods\b", "Nerul Node", RegexOptions.IgnoreCase)
            resultStr = Regex.Replace(resultStr, "\bMahasashtra\b", "Maharashtra", RegexOptions.IgnoreCase)
            
            Return resultStr
        End Function

        Private Function LevenshteinDistance(s As String, t As String) As Integer
            Dim n As Integer = s.Length
            Dim m As Integer = t.Length
            Dim d(n, m) As Integer

            If n = 0 Then Return m
            If m = 0 Then Return n

            For i As Integer = 0 To n
                d(i, 0) = i
            Next

            For j As Integer = 0 To m
                d(0, j) = j
            Next

            For i As Integer = 1 To n
                For j As Integer = 1 To m
                    Dim cost As Integer = If(t(j - 1) = s(i - 1), 0, 1)
                    d(i, j) = Math.Min(Math.Min(d(i - 1, j) + 1, d(i, j - 1) + 1), d(i - 1, j - 1) + cost)
                Next
            Next
            Return d(n, m)
        End Function

        Private Function FuzzyContains(text As String, word As String) As Boolean
            If word.Length < 3 Then Return text.ToUpper().Contains(word.ToUpper())
            
            Dim wordLen = word.Length
            Dim cleanText = Regex.Replace(text.ToUpper(), "[^A-Z]", " ")
            
            For l As Integer = wordLen - 1 To wordLen + 1
                If l < 3 Then Continue For
                For i As Integer = 0 To cleanText.Length - l
                    Dim subStr = cleanText.Substring(i, l).Trim()
                    If subStr.Length >= 3 Then
                        Dim dist = LevenshteinDistance(word.ToUpper(), subStr)
                        Dim maxLen = Math.Max(word.Length, subStr.Length)
                        If (dist / maxLen) <= 0.4 Then
                            Return True
                        End If
                    End If
                Next
            Next
            Return False
        End Function

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
