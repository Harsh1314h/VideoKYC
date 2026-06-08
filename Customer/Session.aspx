<%@ Page Language="VB" AutoEventWireup="false" CodeBehind="Session.aspx.vb" Inherits="VideoKYC.CustomerSession" %>

<!DOCTYPE html>
<html lang="en">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Video KYC Call - Live Verification</title>
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
                <span class="navbar-brand mb-0 h1 fw-bold fs-4 text-gradient">Video KYC Portal</span>
                <span class="d-flex align-items-center">
                    <span class="pulse-circle me-2"></span>
                    <span class="text-secondary-light me-3">Live Call Session</span>
                    <a href="Register.aspx?action=logout" onclick="return confirm('Are you sure you want to end this call?');" class="btn btn-outline-danger btn-sm rounded-pill px-3">End Call</a>
                </span>
            </div>
        </nav>

        <div class="container-fluid py-4 px-4">
            <div class="row g-4">
                <!-- Left: Video Call Screen -->
                <div class="col-lg-8">
                    <div class="video-wrapper">
                        <!-- Main View: Remote Officer Video -->
                        <video id="remoteVideo" class="remote-video" autoplay playsinline></video>
                        
                        <!-- Mini PIP View: Local Customer Video -->
                        <div class="local-video-pip">
                            <video id="localVideo" autoplay playsinline muted></video>
                        </div>
                        
                        <!-- Connection Status Alert overlay -->
                        <div id="statusOverlay" class="position-absolute top-50 start-50 translate-middle text-center p-4 bg-dark bg-opacity-75 rounded-4 border border-secondary-light border-opacity-10" style="max-width: 400px; z-index: 5;">
                            <div class="spinner-border text-primary mb-3" role="status">
                                <span class="visually-hidden">Loading...</span>
                            </div>
                            <h5 id="statusMsg" class="fw-bold mb-1 text-primary">Initializing Camera...</h5>
                            <p class="text-secondary-light mb-0 fs-7">Please grant camera and microphone permissions when prompted.</p>
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

                <!-- Right: Instruction & Action Panel -->
                <div class="col-lg-4">
                    <div class="glass-card p-4 h-100 d-flex flex-column justify-content-between" style="min-height: 480px;">
                        
                        <!-- Panel Header -->
                        <div class="panel-header border-bottom border-secondary-light border-opacity-10 pb-3 mb-3">
                            <h4 class="fw-bold mb-1 text-gradient">Instruction Panel</h4>
                            <p class="text-secondary-light mb-0 fs-7">Follow the prompts triggered by your verification officer.</p>
                        </div>

                        <!-- Panel Body / Dynamic Verification Instructions -->
                        <div class="panel-body flex-grow-1">
                            
                            <!-- Static Initial Prompt -->
                            <div id="pnlDefaultInstruction" class="text-center py-5">
                                <div class="icon-circle bg-primary-soft mx-auto mb-3">
                                    <svg xmlns="http://www.w3.org/2000/svg" width="28" height="28" fill="currentColor" class="bi bi-chat-right-text-fill" viewBox="0 0 16 16">
                                      <path d="M16 2a2 2 0 0 0-2-2H2a2 2 0 0 0-2 2v8a2 2 0 0 0 2 2h9.586a1 1 0 0 1 .707.293l2.853 2.853a.5.5 0 0 0 .854-.353zM3.5 3h9a.5.5 0 0 1 0 1h-9a.5.5 0 0 1 0-1m0 2.5h9a.5.5 0 0 1 0 1h-9a.5.5 0 0 1 0-1m0 2.5h5a.5.5 0 0 1 0 1h-5a.5.5 0 0 1 0-1"/>
                                    </svg>
                                </div>
                                <h5 class="fw-bold text-gradient">Waiting for verification prompts...</h5>
                                <p class="text-secondary-light fs-7 px-4">Once the call connects, the officer will request face, voice, or document uploads here.</p>
                            </div>

                            <!-- Document Upload Instruction Panel -->
                            <div id="pnlDocumentUpload" class="instruction-block d-none">
                                <div class="alert alert-info border-0 bg-primary bg-opacity-10 text-primary p-3 rounded-3 mb-4 fs-7">
                                    <strong>Document Verification Request:</strong> Please upload or capture your original <strong id="lblRequestedDocType">PAN</strong> card.
                                </div>
                                <div class="upload-container border border-dashed border-secondary-light border-opacity-25 rounded-3 p-4 text-center">
                                    <input type="file" id="docUploadInput" class="d-none" accept="image/*,application/pdf" />
                                    <label for="docUploadInput" class="btn btn-outline-accent py-2 px-4 rounded-3 fs-7 fw-semibold cursor-pointer mb-2">
                                        Select Document File
                                    </label>
                                    <p class="text-secondary-light mb-0 fs-8">PNG, JPG, JPEG, or PDF file. Ensure details are visible.</p>
                                </div>
                                <div id="docUploadProgress" class="d-none mt-3">
                                    <div class="progress bg-dark" style="height: 6px;">
                                        <div class="progress-bar bg-primary-gradient progress-bar-striped progress-bar-animated" style="width: 100%;"></div>
                                    </div>
                                    <p class="text-secondary-light fs-8 mt-1 text-center">Uploading and analyzing document text...</p>
                                </div>
                                <div class="mt-3 text-center d-none" id="pnlDocPreview">
                                    <img id="docPhotoPreview" src="#" alt="Doc Preview" class="img-thumbnail bg-dark border-secondary-light" style="max-height: 120px;" />
                                </div>
                            </div>

                            <!-- Voice Capture Instruction Panel -->
                            <div id="pnlVoiceCapture" class="instruction-block d-none">
                                <div class="alert alert-info border-0 bg-primary bg-opacity-10 text-primary p-3 rounded-3 mb-4 fs-7">
                                    <strong>Voice Verification Request:</strong> Click the record button and read the challenge phrase aloud.
                                </div>
                                <div class="phrase-container bg-dark bg-opacity-50 p-4 border border-secondary-light border-opacity-10 rounded-3 text-center mb-4">
                                    <span class="text-secondary-light fs-8 d-block mb-1 text-uppercase tracking-wider">Please Speak:</span>
                                    <h4 class="fw-bold text-white mb-0" id="voicePhrase">Loading challenge phrase...</h4>
                                </div>
                                <div class="text-center mb-3">
                                    <button type="button" id="btnRecordVoice" class="btn btn-primary-gradient px-4 py-3 rounded-pill fw-bold">
                                        Start Recording
                                    </button>
                                </div>
                                <div id="voiceInstructions" class="text-center d-none">
                                    <div class="spinner-grow text-success spinner-grow-sm me-2" role="status"></div>
                                    <span class="text-success fs-7">Recording voice clip... Please read phrase.</span>
                                </div>
                                <div class="text-center mt-3 fs-8 text-secondary-light" id="spokenTextDisplayContainer">
                                    Spoken Text: "<strong class="text-white" id="spokenTextDisplay">--</strong>"
                                </div>
                            </div>

                            <!-- Face Capture Instruction Panel -->
                            <div id="pnlFaceCapture" class="instruction-block d-none">
                                <div class="alert alert-info border-0 bg-primary bg-opacity-10 text-primary p-3 rounded-3 mb-4 fs-7">
                                    <strong>Face Verification:</strong> Align your face in the center of the camera frame.
                                </div>
                                <div class="text-center py-4">
                                    <span id="instructionMsg" class="text-primary fw-semibold fs-6">Looking for face...</span>
                                    <canvas id="captureCanvas" width="320" height="240" class="d-none mt-3 border border-secondary rounded"></canvas>
                                </div>
                            </div>

                            <!-- Verification Result Alert -->
                            <div id="pnlResultAlert" class="d-none mt-4 p-4 text-center rounded-3">
                                <h3 id="resultTitle" class="fw-bold mb-2">Approved</h3>
                                <p id="resultDesc" class="mb-0 fs-7"></p>
                            </div>

                        </div>

                        <!-- Panel Footer -->
                        <div class="panel-footer border-top border-secondary-light border-opacity-10 pt-3 text-center">
                            <span class="text-secondary-light fs-8">Session ID: <strong id="lblDisplaySessionId">--</strong></span>
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

    <!-- ML dependencies -->
    <script src="../Scripts/vendor/face-api.js"></script>

    <!-- Client-side Custom WebRTC Logic -->
    <script src="../Scripts/face-verification.js?v=<%=DateTime.Now.Ticks%>"></script>
    <script src="../Scripts/voice-verification.js?v=<%=DateTime.Now.Ticks%>"></script>
    <script src="../Scripts/document-upload.js?v=<%=DateTime.Now.Ticks%>"></script>
    <script src="../Scripts/webrtc-customer.js?v=<%=DateTime.Now.Ticks%>"></script>
</body>
</html>
