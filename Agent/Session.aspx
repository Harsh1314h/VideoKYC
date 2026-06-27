<%@ Page Language="VB" AutoEventWireup="false" CodeBehind="Session.aspx.vb" Inherits="VideoKYC.AgentSession" %>

<!DOCTYPE html>
<html lang="en">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>KYC Officer Verification Dashboard - Live Session</title>
    <!-- Google Fonts -->
    <link href="https://fonts.googleapis.com/css2?family=Outfit:wght@300;400;600;800&family=Inter:wght@300;400;500;600&display=swap" rel="stylesheet" />
    <!-- Bootstrap 5 CSS -->
    <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css" rel="stylesheet" />
    <!-- Custom Style -->
    <link href="../Styles/kyc-app.css" rel="stylesheet" />
</head>
<body class="bg-dark text-white">
    <form id="form1" runat="server">
        <nav class="navbar navbar-dark bg-dark border-bottom border-secondary-light border-opacity-10 py-3">
            <div class="container-fluid px-4">
                <span class="navbar-brand mb-0 h1 fw-bold fs-4 text-gradient">Officer Control Room</span>
                <span class="d-flex align-items-center">
                    <span class="pulse-circle me-2"></span>
                    <span class="text-secondary-light me-3">Session: <strong class="text-white" id="lblTitleSessionId">--</strong></span>
                    <span class="text-secondary-light me-3">Customer: <strong class="text-white"><asp:Label ID="lblCustName" runat="server">--</asp:Label></strong></span>
                    <a href="Queue.aspx?action=leave&sid=<%=Request.QueryString("sid")%>" class="btn btn-outline-danger btn-sm rounded-pill px-3">Leave Session</a>
                </span>
            </div>
        </nav>

        <div class="container-fluid py-4 px-4">
            <div class="row g-4">
                <!-- Left: Video Call Screen -->
                <div class="col-lg-7">
                    <div class="video-wrapper">
                        <!-- Main View: Remote Customer Video -->
                        <video id="customerVideo" class="remote-video" autoplay playsinline></video>
                        
                        <!-- Mini PIP View: Local Agent Video -->
                        <div class="local-video-pip">
                            <video id="agentVideo" autoplay playsinline muted></video>
                        </div>
                        
                        <!-- Connecting Status Alert Overlay -->
                        <div id="statusOverlay" class="position-absolute top-50 start-50 translate-middle text-center p-4 bg-dark bg-opacity-75 rounded-4 border border-secondary-light border-opacity-10" style="max-width: 400px; z-index: 5;">
                            <div id="statusSpinner" class="spinner-border text-primary mb-3" role="status">
                                <span class="visually-hidden">Loading...</span>
                            </div>
                            <h5 id="statusMsg" class="fw-bold mb-1 text-primary">Awaiting Customer Connection...</h5>
                            <p id="statusDetails" class="text-secondary-light mb-0 fs-7">Establishing peer-to-peer WebRTC video tunnel.</p>
                        </div>
                    </div>

                    <!-- Call Control Bar -->
                    <div class="d-flex justify-content-center gap-3 mt-3 p-3 bg-dark bg-opacity-50 border border-secondary-light border-opacity-10 rounded-3">
                        <button type="button" id="btnMute" class="btn btn-outline-light rounded-circle p-3" title="Mute Microphone">
                            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" fill="currentColor" class="bi bi-mic-fill" viewBox="0 0 16 16">
                              <path d="M5 3a3 3 0 0 1 6 0v5a3 3 0 0 1-6 0z"/>
                              <path d="M3.5 6.5A.5.5 0 0 1 4 7v1a4 4 0 0 0 8 0V7a.5.5 0 0 1 1 0v1a5 5 0 0 1-4.5 4.975V15h3a.5.5 0 0 1 0 1h-7a.5.5 0 0 1 0-1h3v-2.025A5 5 0 0 1 3 8V7a.5.5 0 0 1 .5-.5"/>
                            </svg>
                        </button>
                        <button type="button" id="btnCamOff" class="btn btn-outline-light rounded-circle p-3" title="Turn Camera Off">
                            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" fill="currentColor" class="bi bi-camera-video-fill" viewBox="0 0 16 16">
                              <path fill-rule="evenodd" d="M0 5a2 2 0 0 1 2-2h7.5a2 2 0 0 1 1.983 1.738l3.11-1.382A1 1 0 0 1 16 4.269v7.462a1 1 0 0 1-1.406.913l-3.111-1.382A2 2 0 0 1 9.5 13H2a2 2 0 0 1-2-2zm11.5 5.175 3.5 1.556V4.269l-3.5 1.556z"/>
                            </svg>
                        </button>
                    </div>
                </div>

                <!-- Right: Officer Verification & Audit Console -->
                <div class="col-lg-5">
                    <div class="glass-card p-4 h-100 d-flex flex-column justify-content-between" style="min-height: 520px; overflow-y: auto;">
                        
                        <!-- Tab Layout for Verification Items -->
                        <div>
                            <!-- Header -->
                            <div class="border-bottom border-secondary-light border-opacity-10 pb-3 mb-4">
                                <h4 class="fw-bold mb-1 text-gradient">Verification Panel</h4>
                                <p class="text-secondary-light mb-0 fs-7">Control checks and audit results in real time.</p>
                            </div>

                            <!-- 1. Document Upload Control -->
                            <div class="mb-4 p-3 bg-dark bg-opacity-20 border border-secondary-light border-opacity-10 rounded-3">
                                <h6 class="fw-bold mb-3 text-gradient">1. Document OCR Extraction</h6>
                                <div class="row g-2 align-items-center mb-3">
                                    <div class="col-sm-7">
                                        <select id="ddlDocType" class="form-select bg-dark border-secondary-light text-white rounded-3 fs-7 py-2">
                                            <option value="Aadhaar">Aadhaar Card</option>
                                            <option value="PAN">PAN Card</option>
                                            <option value="Passport">Passport</option>
                                            <option value="DL">Driving Licence</option>
                                        </select>
                                    </div>
                                    <div class="col-sm-5">
                                        <button type="button" onclick="triggerDocument(document.getElementById('ddlDocType').value)" class="btn btn-primary-gradient w-100 py-2 fs-7 fw-semibold">
                                            Request Upload
                                        </button>
                                    </div>
                                </div>
                                <div class="extracted-details bg-dark bg-opacity-50 p-3 rounded border border-secondary-light border-opacity-5">
                                    <span class="text-secondary-light fs-8 d-block mb-1">Extracted Details:</span>
                                    <span class="badge bg-secondary mb-2" id="docStatus">Pending...</span>
                                    <div id="docExtractedData" class="fs-8 text-secondary-light">
                                        No data received yet.
                                    </div>
                                </div>
                            </div>

                            <!-- 2. Face Verification Control -->
                            <div class="mb-4 p-3 bg-dark bg-opacity-20 border border-secondary-light border-opacity-10 rounded-3">
                                <h6 class="fw-bold mb-2 text-gradient">2. Biometric Face Match</h6>
                                <div class="d-flex justify-content-between align-items-center mb-3">
                                    <span class="fs-7 text-secondary-light">Similarity Threshold: <strong>50%</strong></span>
                                    <button type="button" onclick="triggerFace()" class="btn btn-primary-gradient py-2 px-4 fs-7 fw-semibold">
                                        Capture Face
                                    </button>
                                </div>
                                <div class="row g-3 text-center">
                                    <div class="col-6 border-end border-secondary-light border-opacity-10">
                                        <span class="text-secondary-light fs-8 d-block">Similarity Score</span>
                                        <h3 class="fw-bold mb-0 text-white" id="faceScore">--</h3>
                                    </div>
                                    <div class="col-6">
                                        <span class="text-secondary-light fs-8 d-block">Face Match Status</span>
                                        <span class="badge bg-secondary mt-1" id="faceStatus">Pending...</span>
                                    </div>
                                </div>
                            </div>

                            <!-- 3. Voice Verification Control -->
                            <div class="mb-4 p-3 bg-dark bg-opacity-20 border border-secondary-light border-opacity-10 rounded-3">
                                <h6 class="fw-bold mb-2 text-gradient">3. Voice Challenge-Phrase</h6>
                                <div class="row g-2 align-items-center mb-3">
                                    <div class="col-sm-7">
                                        <select id="ddlVoicePhrase" class="form-select bg-dark border-secondary-light text-white rounded-3 fs-7 py-2">
                                            <option id="optVoiceName" value="name">My name is [Customer Name]</option>
                                            <option value="authorize">I authorize this KYC process</option>
                                        </select>
                                    </div>
                                    <div class="col-sm-5">
                                        <button type="button" onclick="triggerVoiceChallenge()" class="btn btn-primary-gradient w-100 py-2 fs-7 fw-semibold">
                                            Voice Challenge
                                        </button>
                                    </div>
                                </div>
                                <div class="bg-dark bg-opacity-50 p-3 rounded mb-3 border border-secondary-light border-opacity-5">
                                    <div class="row g-3 text-center">
                                        <div class="col-6 border-end border-secondary-light border-opacity-10">
                                            <span class="text-secondary-light fs-8 d-block">Overall Score</span>
                                            <h3 class="fw-bold mb-0 text-white" id="voiceScore">--</h3>
                                        </div>
                                        <div class="col-6">
                                            <span class="text-secondary-light fs-8 d-block">Status</span>
                                            <span class="badge bg-secondary mt-1" id="voiceStatus">Pending...</span>
                                        </div>
                                    </div>
                                </div>
                                <div class="fs-8 text-secondary-light">
                                    Spoken Text: "<strong class="text-white" id="voiceSpoken">--</strong>"
                                </div>
                            </div>

                        </div>

                        <!-- 4. Final KYC Decisions -->
                        <div class="border-top border-secondary-light border-opacity-10 pt-4 mt-3">
                            <div class="mb-3">
                                <label for="rejectionReason" class="form-label text-secondary-light fs-7">Rejection Reason (Required if Rejecting)</label>
                                <textarea id="rejectionReason" class="form-control bg-dark border-secondary-light text-white fs-7" rows="2" placeholder="Describe the reason for rejection..."></textarea>
                            </div>
                            <div class="row g-3">
                                <div class="col-6">
                                    <button type="button" onclick="rejectKyc()" class="btn btn-outline-danger w-100 py-3 fw-bold rounded-3">
                                        &#10007; Reject KYC
                                    </button>
                                </div>
                                <div class="col-6">
                                    <button type="button" onclick="approveKyc()" class="btn btn-success w-100 py-3 fw-bold text-dark rounded-3 bg-success border-0 shadow">
                                        &#10003; Approve KYC
                                    </button>
                                </div>
                            </div>
                        </div>

                    </div>
                </div>
            </div>
        </div>

        <asp:HiddenField ID="hdnSessionId" runat="server" ClientIDMode="Static" />
    </form>

    <!-- jQuery & SignalR vendor dependencies -->
    <script src="https://code.jquery.com/jquery-3.7.1.min.js"></script>
    <script src="../Scripts/vendor/jquery.signalR-2.4.3.min.js"></script>
    <script src="/signalr/hubs"></script>

    <!-- Client-side Custom WebRTC Logic -->
    <script src="../Scripts/webrtc-agent.js?v=<%=DateTime.Now.Ticks%>"></script>
</body>
</html>
