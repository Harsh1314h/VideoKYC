<%@ Page Language="VB" AutoEventWireup="false" CodeBehind="Login.aspx.vb" Inherits="VideoKYC.Login" %>

<!DOCTYPE html>
<html lang="en">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Officer Login - Video KYC</title>
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

            <div class="container position-relative z-1" style="max-width: 450px;">
                <div class="text-center mb-4">
                    <a href="../Default.aspx" class="text-decoration-none text-secondary hover-glow mb-3 d-inline-block">
                        &larr; Back to Home
                    </a>
                    <h2 class="fw-extrabold text-gradient">Officer Portal</h2>
                    <p class="text-secondary-light">Log in to process customer verification sessions.</p>
                </div>

                <div class="glass-card p-5">
                    <asp:Panel ID="pnlError" runat="server" Visible="false" CssClass="alert alert-danger border-0 bg-danger bg-opacity-10 text-danger rounded-3 mb-4">
                        <asp:Label ID="lblError" runat="server"></asp:Label>
                    </asp:Panel>

                    <div class="mb-4">
                        <label for="txtUsername" class="form-label text-secondary-light fw-medium">Officer Username</label>
                        <asp:TextBox ID="txtUsername" runat="server" CssClass="form-control bg-dark border-secondary-light text-white py-3 rounded-3" placeholder="Enter username (e.g. officer1)" required="required"></asp:TextBox>
                    </div>

                    <div class="mb-5">
                        <label for="txtPassword" class="form-label text-secondary-light fw-medium">Password</label>
                        <asp:TextBox ID="txtPassword" runat="server" ClientIDMode="Static" CssClass="form-control bg-dark border-secondary-light text-white py-3 rounded-3" TextMode="Password" placeholder="Enter password (e.g. password123)" required="required"></asp:TextBox>
                        <div class="form-check mt-3">
                            <input class="form-check-input bg-dark border-secondary-light" type="checkbox" id="chkShowPassword">
                            <label class="form-check-label text-secondary-light fs-7" for="chkShowPassword">
                                Show Password
                            </label>
                        </div>
                    </div>

                    <asp:Button ID="btnLogin" runat="server" OnClick="btnLogin_Click" CssClass="btn btn-primary-gradient w-100 py-3 fw-bold rounded-3 fs-5" Text="Log In" />
                </div>
            </div>
        </div>
    </form>

    <!-- jQuery & Toggle Script -->
    <script src="https://code.jquery.com/jquery-3.7.1.min.js"></script>
    <script>
        $(document).ready(function () {
            $('#chkShowPassword').change(function () {
                var passwordInput = $('#txtPassword');
                if ($(this).is(':checked')) {
                    passwordInput.attr('type', 'text');
                } else {
                    passwordInput.attr('type', 'password');
                }
            });
        });
    </script>
</body>
</html>
