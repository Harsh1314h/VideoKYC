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

                    <!-- Nav Tabs / Switcher to toggle between "Register" and "Rejoin" -->
                    <ul class="nav nav-pills nav-fill mb-4 p-1 bg-dark bg-opacity-50 border border-secondary-light border-opacity-10 rounded-pill" role="tablist">
                        <li class="nav-item">
                            <button type="button" class="nav-link active rounded-pill py-2 fw-semibold fs-7" id="btnTabRegister" data-bs-toggle="pill" data-bs-target="#pnlRegister" role="tab">New Verification</button>
                        </li>
                        <li class="nav-item">
                            <button type="button" class="nav-link rounded-pill py-2 fw-semibold fs-7" id="btnTabRejoin" data-bs-toggle="pill" data-bs-target="#pnlRejoin" role="tab">Rejoin Call</button>
                        </li>
                    </ul>

                    <div class="tab-content">
                        <!-- Panel: Register -->
                        <div class="tab-pane fade show active" id="pnlRegister" role="tabpanel">
                            <div class="mb-4">
                                <label for="txtFullName" class="form-label text-secondary-light fw-medium">Full Name</label>
                                <asp:TextBox ID="txtFullName" runat="server" CssClass="form-control bg-dark border-secondary-light text-white py-3 rounded-3" placeholder="Enter your full name as on ID card" MaxLength="50"></asp:TextBox>
                            </div>

                            <div class="mb-4">
                                <label for="txtPhone" class="form-label text-secondary-light fw-medium">Mobile Number</label>
                                <asp:TextBox ID="txtPhone" runat="server" ClientIDMode="Static" CssClass="form-control bg-dark border-secondary-light text-white py-3 rounded-3" placeholder="Enter 10-digit mobile number" MaxLength="10" onkeypress="return isNumberKey(event)"></asp:TextBox>
                            </div>

                            <asp:Button ID="btnSubmit" runat="server" OnClick="btnSubmit_Click" CssClass="btn btn-primary-gradient w-100 py-3 fw-bold rounded-3 fs-5" Text="Start Verification" />
                        </div>

                        <!-- Panel: Rejoin -->
                        <div class="tab-pane fade" id="pnlRejoin" role="tabpanel">
                            <div class="mb-4">
                                <label for="txtSessionId" class="form-label text-secondary-light fw-medium">Verification Session ID</label>
                                <asp:TextBox ID="txtSessionId" runat="server" CssClass="form-control bg-dark border-secondary-light text-white py-3 rounded-3" placeholder="Enter your unique Session ID (GUID)"></asp:TextBox>
                            </div>

                            <asp:Button ID="btnRejoin" runat="server" OnClick="btnRejoin_Click" CssClass="btn btn-outline-accent w-100 py-3 fw-bold rounded-3 fs-5" Text="Rejoin Session" />
                        </div>
                    </div>
                </div>
            </div>
        </div>
        <asp:HiddenField ID="hdnActiveTab" runat="server" Value="register" ClientIDMode="Static" />
    </form>

    <!-- jQuery & Bootstrap 5 JS -->
    <script src="https://code.jquery.com/jquery-3.7.1.min.js"></script>
    <script src="https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/js/bootstrap.bundle.min.js"></script>
    <script>
        function isNumberKey(evt) {
            var charCode = (evt.which) ? evt.which : evt.keyCode;
            if (charCode > 31 && (charCode < 48 || charCode > 57)) {
                return false;
            }
            return true;
        }

        $(document).ready(function () {
            var activeTab = $('#hdnActiveTab').val();
            if (activeTab === 'rejoin') {
                var triggerEl = document.querySelector('#btnTabRejoin');
                if (triggerEl) {
                    var tab = bootstrap.Tab.getOrCreateInstance(triggerEl);
                    tab.show();
                }
            }

            $('#btnTabRegister').on('shown.bs.tab', function () {
                $('#hdnActiveTab').val('register');
            });
            $('#btnTabRejoin').on('shown.bs.tab', function () {
                $('#hdnActiveTab').val('rejoin');
            });
        });
    </script>
</body>
</html>
