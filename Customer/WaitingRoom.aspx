<%@ Page Language="VB" AutoEventWireup="false" CodeBehind="WaitingRoom.aspx.vb" Inherits="VideoKYC.WaitingRoom" %>

<!DOCTYPE html>
<html lang="en">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Waiting Room - Video KYC</title>
    <!-- Google Fonts -->
    <link href="https://fonts.googleapis.com/css2?family=Outfit:wght@300;400;600;800&family=Inter:wght@300;400;500;600&display=swap" rel="stylesheet" />
    <!-- Bootstrap 5 CSS -->
    <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css" rel="stylesheet" />
    <!-- Custom Style -->
    <link href="../Styles/kyc-app.css" rel="stylesheet" />
</head>
<body class="bg-dark text-white">
    <form id="form1" runat="server">
        <div class="hero-container d-flex align-items-center justify-content-center min-vh-100 position-relative overflow-hidden">
            <div class="glow-bg position-absolute top-50 start-50 translate-middle"></div>
            
            <div class="container text-center position-relative z-1" style="max-width: 500px;">
                <div class="glass-card p-5">
                    <div class="spinner-container mb-4">
                        <div class="spinner-border text-primary" role="status" style="width: 4rem; height: 4rem;">
                            <span class="visually-hidden">Loading...</span>
                        </div>
                    </div>

                    <h2 class="fw-bold mb-3">Waiting Room</h2>
                    <p class="text-secondary-light fs-6">An agent will join your session shortly. Please do not close or refresh this page.</p>

                    <div class="queue-status mt-4 p-3 bg-dark bg-opacity-50 rounded-3 border border-secondary-light border-opacity-10">
                        <span class="pulse-circle me-2"></span>
                        <span class="text-secondary-light">Status: </span>
                        <strong class="text-primary" id="statusText">Waiting for Agent...</strong>
                    </div>

                    <asp:HiddenField ID="hdnSessionId" runat="server" ClientIDMode="Static" />

                    <div class="mt-4 pt-3 border-top border-secondary-light border-opacity-10">
                        <a href="Register.aspx?action=logout" onclick="return confirm('Are you sure you want to leave the queue?');" class="text-decoration-none text-danger fs-7 hover-glow">
                            Cancel & Leave Queue
                        </a>
                    </div>
                </div>
            </div>
        </div>
    </form>

    <!-- jQuery -->
    <script src="https://code.jquery.com/jquery-3.7.1.min.js"></script>
    <script>
        $(document).ready(function () {
            var sessionId = $('#hdnSessionId').val();
            if (!sessionId) {
                window.location.href = "Register.aspx";
                return;
            }

            // Poll the status every 2 seconds
            var interval = setInterval(function () {
                $.ajax({
                    type: "POST",
                    url: "WaitingRoom.aspx/CheckSessionStatus",
                    data: JSON.stringify({ sessionId: sessionId }),
                    contentType: "application/json; charset=utf-8",
                    dataType: "json",
                    success: function (msg) {
                        var status = msg.d;
                        if (status === "InProgress") {
                            clearInterval(interval);
                            $('#statusText').text("Agent Connected! Joining...").addClass("text-success").removeClass("text-primary");
                            setTimeout(function () {
                                window.location.href = "Session.aspx";
                            }, 1000);
                        } else if (status === "Approved" || status === "Rejected") {
                            clearInterval(interval);
                            window.location.href = "Session.aspx";
                        }
                    },
                    error: function () {
                        console.log("Error checking session status.");
                    }
                });
            }, 2000);
        });
    </script>
</body>
</html>
