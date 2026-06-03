// document-upload.js
var requestedDocType = "Aadhaar";

function startDocumentUpload(docType) {
    console.log("Document upload requested. Type: " + docType);
    requestedDocType = docType;
    $('#lblRequestedDocType').text(docType);
    $('#docUploadProgress').addClass('d-none');
    $('#pnlDocPreview').addClass('d-none');
    $('#docUploadInput').val(''); // Reset file input
}

$(document).ready(function () {
    // Bind file selection
    $('#docUploadInput').on('change', function (e) {
        var file = e.target.files[0];
        if (!file) return;

        // Display progress indicator
        $('#docUploadProgress').removeClass('d-none');

        var fd = new FormData();
        fd.append('document', file);
        fd.append('sessionId', sessionId);
        fd.append('docType', requestedDocType);

        console.log("Uploading document image file for OCR analysis...");

        fetch('/Handlers/UploadDocument.ashx', {
            method: 'POST',
            body: fd
        })
        .then(response => response.json())
        .then(data => {
            console.log("OCR document verification result: ", data);
            
            // Hide progress indicator
            $('#docUploadProgress').addClass('d-none');

            // Render Preview Image in client side for face-api
            var reader = new FileReader();
            reader.onload = function (event) {
                $('#docPhotoPreview').attr('src', event.target.result);
                $('#pnlDocPreview').removeClass('d-none');
            };
            reader.readAsDataURL(file);

            // Send OCR extraction results back to officer
            kycProxy.invoke('sendVerificationResult', sessionId, 'document', JSON.stringify(data));
        })
        .catch(err => {
            console.error("Document upload failed: ", err);
            $('#docUploadProgress').addClass('d-none');
            alert("Document analysis failed. Please check the image and try again.");
        });
    });
});
