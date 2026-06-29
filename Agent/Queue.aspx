<%@ Page Language="VB" AutoEventWireup="false" CodeBehind="Queue.aspx.vb" Inherits="VideoKYC.Queue" %>

<!DOCTYPE html>
<html lang="en">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Active Verification Queue - Video KYC</title>
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
                <span class="navbar-brand mb-0 h1 fw-bold fs-4 text-gradient">Video KYC Portal - Officer Dashboard</span>
                <span class="d-flex align-items-center">
                    <span class="text-secondary-light me-3">Welcome, <strong class="text-white"><asp:Label ID="lblAgentName" runat="server">Officer</asp:Label></strong></span>
                    <asp:LinkButton ID="btnLogOut" runat="server" OnClick="btnLogOut_Click" CssClass="btn btn-outline-danger btn-sm rounded-pill px-3">Sign Out</asp:LinkButton>
                </span>
            </div>
        </nav>

        <div class="container py-5" style="max-width: 900px;">
            <div class="glow-bg position-absolute top-50 start-50 translate-middle"></div>
            
            <div class="position-relative z-1">
                <!-- Reconnect Active Sessions Panel -->
                <asp:Panel ID="pnlActiveSessions" runat="server" Visible="false" CssClass="mb-5">
                    <div class="d-flex justify-content-between align-items-center mb-4">
                        <div>
                            <h3 class="fw-bold mb-1 text-success">Your Active Sessions</h3>
                            <p class="text-secondary-light mb-0">You are currently conducting these calls. Click Reconnect to rejoin.</p>
                        </div>
                    </div>
                    <div class="glass-card p-4 border border-success border-opacity-20">
                        <asp:GridView ID="gvActiveSessions" runat="server" AutoGenerateColumns="False" OnRowCommand="gvActiveSessions_RowCommand" 
                                      CssClass="table table-dark table-hover border-0 mb-0" 
                                      GridLines="None" DataKeyNames="SessionId">
                            <Columns>
                                <asp:BoundField DataField="CustomerName" HeaderText="Customer Name" HeaderStyle-CssClass="text-secondary-light fw-semibold" ItemStyle-CssClass="py-3 fs-6" />
                                <asp:BoundField DataField="CustomerPhone" HeaderText="Phone" HeaderStyle-CssClass="text-secondary-light fw-semibold" ItemStyle-CssClass="py-3" />
                                <asp:BoundField DataField="UpdatedAt" HeaderText="Last Heartbeat" HeaderStyle-CssClass="text-secondary-light fw-semibold" ItemStyle-CssClass="py-3 text-secondary-light fs-7" DataFormatString="{0:hh:mm:ss tt}" />
                                <asp:TemplateField HeaderText="Actions" HeaderStyle-CssClass="text-secondary-light fw-semibold text-end" ItemStyle-CssClass="text-end py-3">
                                    <ItemTemplate>
                                        <asp:Button ID="btnReconnect" runat="server" 
                                                    CssClass="btn btn-success w-auto px-4 py-2 fw-semibold rounded-3 text-dark bg-success border-0 shadow" 
                                                    Text="Reconnect" CommandName="ReconnectCall" 
                                                    CommandArgument='<%# Eval("SessionId") %>' />
                                    </ItemTemplate>
                                </asp:TemplateField>
                            </Columns>
                        </asp:GridView>
                    </div>
                </asp:Panel>

                <div class="d-flex justify-content-between align-items-center mb-4">
                    <div>
                        <h2 class="fw-bold mb-1">Incoming Call Queue</h2>
                        <p class="text-secondary-light mb-0">Claim waiting customer sessions to establish verification video feeds.</p>
                    </div>
                    <asp:Button ID="btnRefresh" runat="server" OnClick="btnRefresh_Click" CssClass="btn btn-outline-accent py-2 px-4 rounded-3 fw-semibold" Text="Refresh Queue" />
                </div>

                <div class="glass-card p-4">
                    <asp:GridView ID="gvSessions" runat="server" AutoGenerateColumns="False" OnRowCommand="gvSessions_RowCommand" 
                                  CssClass="table table-dark table-hover border-0 mb-0" 
                                  GridLines="None" DataKeyNames="SessionId">
                        <Columns>
                            <asp:BoundField DataField="CustomerName" HeaderText="Customer Name" HeaderStyle-CssClass="text-secondary-light fw-semibold" ItemStyle-CssClass="py-3 fs-6" />
                            <asp:BoundField DataField="CustomerPhone" HeaderText="Phone" HeaderStyle-CssClass="text-secondary-light fw-semibold" ItemStyle-CssClass="py-3" />
                            <asp:BoundField DataField="CreatedAt" HeaderText="Registered At" HeaderStyle-CssClass="text-secondary-light fw-semibold" ItemStyle-CssClass="py-3 text-secondary-light fs-7" DataFormatString="{0:hh:mm:ss tt}" />
                            <asp:TemplateField HeaderText="Actions" HeaderStyle-CssClass="text-secondary-light fw-semibold text-end" ItemStyle-CssClass="text-end py-3">
                                <ItemTemplate>
                                    <asp:Button ID="btnJoin" runat="server" 
                                                CssClass="btn btn-primary-gradient btn-sm px-4 py-2 fw-semibold rounded-3" 
                                                Text="Join Call" CommandName="JoinCall" 
                                                CommandArgument='<%# Eval("SessionId") %>' />
                                </ItemTemplate>
                            </asp:TemplateField>
                        </Columns>
                        <EmptyDataTemplate>
                            <div class="text-center py-5">
                                <div class="icon-circle bg-accent-soft mx-auto mb-3">
                                    <svg xmlns="http://www.w3.org/2000/svg" width="28" height="28" fill="currentColor" class="bi bi-inbox-fill" viewBox="0 0 16 16">
                                      <path d="M4.98 4a.5.5 0 0 0-.39.188L1.54 8H6a.5.5 0 0 1 .5.5 1.5 1.5 0 1 0 3 0A.5.5 0 0 1 10 8h4.46l-3.05-3.812A.5.5 0 0 0 11.02 4zm9.96 4.99H11.8a2.5 2.5 0 0 1-4.6 0H1.06a.5.5 0 0 0-.06.18l.75 3.75a1 1 0 0 0 .98.8H13.25a1 1 0 0 0 .98-.8l.75-3.75a.5.5 0 0 0-.04-.18"/>
                                    </svg>
                                </div>
                                <h5 class="fw-bold text-secondary-light">Queue is empty</h5>
                                <p class="text-secondary mb-0 fs-7">No customers are currently waiting for verification. Check back shortly.</p>
                            </div>
                        </EmptyDataTemplate>
                    </asp:GridView>
                </div>

                <!-- Approved KYC History Panel -->
                <div class="d-flex justify-content-between align-items-center mt-5 mb-4">
                    <div>
                        <h2 class="fw-bold mb-1 text-primary">Approved KYC History</h2>
                        <p class="text-secondary-light mb-0">List of all successfully completed and approved video verification sessions.</p>
                    </div>
                </div>

                <div class="glass-card p-4">
                    <asp:GridView ID="gvApproved" runat="server" AutoGenerateColumns="False" 
                                  CssClass="table table-dark table-hover border-0 mb-0" 
                                  GridLines="None" DataKeyNames="SessionId">
                        <Columns>
                            <asp:BoundField DataField="CustomerName" HeaderText="Customer Name" HeaderStyle-CssClass="text-secondary-light fw-semibold" ItemStyle-CssClass="py-3 fs-6" />
                            <asp:BoundField DataField="CustomerPhone" HeaderText="Phone" HeaderStyle-CssClass="text-secondary-light fw-semibold" ItemStyle-CssClass="py-3" />
                            <asp:BoundField DataField="UpdatedAt" HeaderText="Approved At" HeaderStyle-CssClass="text-secondary-light fw-semibold" ItemStyle-CssClass="py-3 text-secondary-light fs-7" DataFormatString="{0:hh:mm:ss tt}" />
                            <asp:TemplateField HeaderText="Status" HeaderStyle-CssClass="text-secondary-light fw-semibold text-end" ItemStyle-CssClass="text-end py-3">
                                <ItemTemplate>
                                    <span class="badge bg-success text-dark px-3 py-2 rounded-pill fw-bold">Approved ✓</span>
                                </ItemTemplate>
                            </asp:TemplateField>
                        </Columns>
                        <EmptyDataTemplate>
                            <div class="text-center py-4">
                                <h5 class="fw-bold text-secondary-light mb-0">No approved KYC records found</h5>
                            </div>
                        </EmptyDataTemplate>
                    </asp:GridView>
                </div>
            </div>
        </div>
    </form>
</body>
</html>
