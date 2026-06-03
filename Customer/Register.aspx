<%@ Page Language="VB" AutoEventWireup="false" CodeBehind="Register.aspx.vb" Inherits="VideoKYC.Register" %>

<!DOCTYPE html>
<html lang="en">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Customer Registration - Video KYC</title>
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

            <div class="container position-relative z-1" style="max-width: 500px;">
                <div class="text-center mb-4">
                    <a href="../Default.aspx" class="text-decoration-none text-secondary hover-glow mb-3 d-inline-block">
                        &larr; Back to Home
                    </a>
                    <h2 class="fw-extrabold">Start Verification</h2>
                    <p class="text-secondary-light">Provide your basic details to start a secure video call.</p>
                </div>

                <div class="glass-card p-5">
                    <asp:Panel ID="pnlError" runat="server" Visible="false" CssClass="alert alert-danger border-0 bg-danger bg-opacity-10 text-danger rounded-3 mb-4">
                        <asp:Label ID="lblError" runat="server"></asp:Label>
                    </asp:Panel>

                    <div class="mb-4">
                        <label for="txtFullName" class="form-label text-secondary-light fw-medium">Full Name</label>
                        <asp:TextBox ID="txtFullName" runat="server" CssClass="form-control bg-dark border-secondary-light text-white py-3 rounded-3" placeholder="Enter your full name as on ID card" required="required"></asp:TextBox>
                    </div>

                    <div class="mb-5">
                        <label for="txtPhone" class="form-label text-secondary-light fw-medium">Mobile Number</label>
                        <asp:TextBox ID="txtPhone" runat="server" CssClass="form-control bg-dark border-secondary-light text-white py-3 rounded-3" placeholder="Enter 10-digit mobile number" required="required" MaxLength="15"></asp:TextBox>
                    </div>

                    <asp:Button ID="btnSubmit" runat="server" OnClick="btnSubmit_Click" CssClass="btn btn-primary-gradient w-100 py-3 fw-bold rounded-3 fs-5" Text="Start Verification" />
                </div>
            </div>
        </div>
    </form>
</body>
</html>
