<%@ Page Language="VB" AutoEventWireup="false" CodeBehind="Default.aspx.vb" Inherits="VideoKYC._Default" %>

<!DOCTYPE html>
<html lang="en">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Video KYC Portal - Home</title>
    <!-- Google Fonts -->
    <link href="https://fonts.googleapis.com/css2?family=Outfit:wght@300;400;600;800&family=Inter:wght@300;400;500;600&display=swap" rel="stylesheet" />
    <!-- Bootstrap 5 CSS -->
    <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css" rel="stylesheet" />
    <!-- Custom Style -->
    <link href="Styles/kyc-app.css" rel="stylesheet" />
</head>
<body class="bg-dark text-white">
    <form id="form1" runat="server">
        <div class="hero-container d-flex align-items-center justify-content-center min-vh-100 position-relative overflow-hidden">
            <div class="glow-bg position-absolute top-50 start-50 translate-middle"></div>
            
            <div class="container text-center position-relative z-1" style="max-width: 800px;">
                <div class="brand mb-4">
                    <span class="badge bg-primary-gradient px-3 py-2 rounded-pill fs-7 mb-3 text-uppercase tracking-wider">Secure e-Verification</span>
                    <h1 class="display-3 fw-extrabold tracking-tight">Video <span class="text-gradient">KYC</span> Portal</h1>
                    <p class="lead text-secondary-light">Instant identity verification through secure, real-time video calls powered by AI.</p>
                </div>

                <div class="row g-4 mt-5">
                    <div class="col-md-6">
                        <div class="glass-card hover-glow p-5 h-100 d-flex flex-column align-items-center justify-content-between">
                            <div class="card-icon mb-4">
                                <div class="icon-circle bg-primary-soft">
                                    <svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" fill="currentColor" class="bi bi-person-fill" viewBox="0 0 16 16">
                                      <path d="M3 14s-1 0-1-1 1-4 6-4 6 3 6 4-1 1-1 1zm5-6a3 3 0 1 0 0-6 3 3 0 0 0 0 6"/>
                                    </svg>
                                </div>
                            </div>
                            <div>
                                <h3 class="fw-bold mb-3">Customer Portal</h3>
                                <p class="text-secondary-light fs-6">Complete your identity verification in minutes. Register and start a video call with a KYC officer.</p>
                            </div>
                            <div class="w-100 mt-4">
                                <a href="Customer/Register.aspx" class="btn btn-primary-gradient w-100 py-3 fw-semibold">Start Verification</a>
                            </div>
                        </div>
                    </div>

                    <div class="col-md-6">
                        <div class="glass-card hover-glow p-5 h-100 d-flex flex-column align-items-center justify-content-between">
                            <div class="card-icon mb-4">
                                <div class="icon-circle bg-accent-soft">
                                    <svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" fill="currentColor" class="bi bi-shield-lock-fill" viewBox="0 0 16 16">
                                      <path fill-rule="evenodd" d="M8 0c-.69 0-1.843.265-2.928.56-1.11.3-2.229.655-2.887.87a1.54 1.54 0 0 0-1.044 1.262c-.596 4.477.787 7.795 2.465 9.99a11.8 11.8 0 0 0 2.517 2.453c.386.273.744.482 1.048.625.28.132.581.24.829.24s.548-.108.829-.24c.304-.143.662-.352 1.048-.625a11.8 11.8 0 0 0 2.517-2.453c1.678-2.195 3.061-5.513 2.465-9.99a1.54 1.54 0 0 0-1.044-1.263 63 63 0 0 0-2.887-.87C9.843.266 8.69 0 8 0m0 5a1.5 1.5 0 0 1 .5 2.915V9a.5.5 0 0 1-1 0V7.915A1.5 1.5 0 0 1 8 5"/>
                                    </svg>
                                </div>
                            </div>
                            <div>
                                <h3 class="fw-bold mb-3">Officer Portal</h3>
                                <p class="text-secondary-light fs-6">KYC Officers and Auditors portal. Log in to review verification queues, answer live calls, and process approvals.</p>
                            </div>
                            <div class="w-100 mt-4">
                                <a href="Agent/Queue.aspx" class="btn btn-outline-accent w-100 py-3 fw-semibold">Login as Officer</a>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    </form>
</body>
</html>
