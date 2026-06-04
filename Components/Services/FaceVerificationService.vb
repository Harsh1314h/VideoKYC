Imports OpenCvSharp

Namespace Services
    Public Class FaceVerificationService
        ''' <summary>
        ''' Compares a live face image and the cropped face photo from a document.
        ''' Returns a similarity score from -100 to +100 (correlation coefficient).
        ''' </summary>
        Public Function CompareFaces(livePath As String, docPath As String) As Double
            Try
                Using liveImg = Cv2.ImRead(livePath, ImreadModes.Grayscale)
                Using docImg = Cv2.ImRead(docPath, ImreadModes.Grayscale)
                    If liveImg.Empty() OrElse docImg.Empty() Then Return 0

                    Using liveResized As New Mat()
                    Using docResized As New Mat()
                        ' Resize to standard resolution for uniform comparison
                        Cv2.Resize(liveImg, liveResized, New Size(200, 200))
                        Cv2.Resize(docImg, docResized, New Size(200, 200))

                        Using liveHist As New Mat()
                        Using docHist As New Mat()
                            Dim channels As Integer() = {0}
                            Dim histSize As Integer() = {256}
                            Dim ranges As Rangef() = {New Rangef(0, 256)}

                            ' Calculate histograms
                            Cv2.CalcHist(New Mat() {liveResized}, channels, Nothing, liveHist, 1, histSize, ranges)
                            Cv2.CalcHist(New Mat() {docResized}, channels, Nothing, docHist, 1, histSize, ranges)

                            ' Normalize histograms
                            Cv2.Normalize(liveHist, liveHist, 0, 1, NormTypes.MinMax)
                            Cv2.Normalize(docHist, docHist, 0, 1, NormTypes.MinMax)

                            ' Compare using correlation method
                            Dim correlation = Cv2.CompareHist(liveHist, docHist, HistCompMethods.Correl)
                            Dim score = correlation * 100

                            ' Cap between 0 and 100
                            If score < 0 Then score = 0
                            Return Math.Round(score, 2)
                        End Using
                        End Using
                    End Using
                    End Using
                End Using
                End Using
            Catch ex As Exception
                ' Fallback in case of exceptions (e.g. invalid files)
                Return 0
            End Try
        End Function
    End Class
End Namespace
