//
// Copyright (C) 2016 by Jeremy Spiller, all rights reserved
//

"use strict"

//
// Request a draw buffer and image.
// Call Start to start the request, and use OnDone to retrieve the result.
//
function WrvRequest()
{
    var THIS = this;
    THIS.SessionId = null;
    THIS.Sequence = null;
    THIS.Image = null;
    THIS.Draw = null;
    THIS.Error = null;

    // Called when Image and Draw have loaded (Error is null if everything is OK)
    THIS.OnDone = function () { }

    // Start downloading the sequence (OnDone is called when done or there is an error)
    THIS.Start = function (sessionId, sequence)
    {
        THIS.SessionId = sessionId;
        THIS.Sequence = sequence;

        // Start downloading the image
        var image = new Image();
        image.onload = function ()
        {
            THIS.Image = image;
            GotData();
        };
        image.onerror = function ()
        {
            SetError("Image");
        };
        image.src = "index.wrv?query=image&sid=" + THIS.SessionId + "&seq=" + THIS.Sequence;

        // Start downloading the draw buffer
        var xhttp = new XMLHttpRequest();
        xhttp.onreadystatechange = function ()
        {
            if (this.readyState != 4)
                return;
            if (this.status != 200)
            {
                SetError("Draw buffer");
                return;
            }
            THIS.Draw = this.responseText;
            GotData();
        };
        xhttp.open("GET", "index.wrv?query=draw&sid=" + THIS.SessionId + "&seq=" + THIS.Sequence, true);
        xhttp.send();
    }

    // Set an eror, call OnDone if it hasn't already been called
    function SetError(error)
    {
        console.log("WrvRequest Error: " + error);

        if (THIS.Error == null)
        {
            THIS.Error = error;
            THIS.Image = null;
            THIS.Draw = null;
            THIS.OnDone();
        }
    }

    // Call OnDone if we have the image and draw buffer and there is no error
    function GotData()
    {
        if (THIS.Image != null && THIS.Draw != null && THIS.Error == null)
        {
            THIS.OnDone();
        }
    }
}

//
// Main remote viewer class, used to continuosly update the canvas.
//
function WebRemoteViewer(drawString, canvas)
{
    var THIS = this;
    var mDrawString = drawString;
    var mCanvas = canvas;
    var mContext = canvas.getContext('2d');
    var mSessionId = 0;
    var mSequence = 1;

    function InitializeCanvas()
    {
        mDrawString.innerHTML = "Starting...";
        mContext.fillStyle = '#228';
        mContext.fillRect(0, 0, mCanvas.width, mCanvas.height);
        mContext.fillStyle = '#fff';
        mContext.font = '60px sans-serif';
        mContext.fillText('Starting...', 10, mCanvas.height / 2 - 15);
    }

    function ShowError(error)
    {
        mDrawString.innerHTML = 'ERROR: ' + error;

        mContext.fillStyle = '#000';
        mContext.fillRect(0, 0, mCanvas.width, mCanvas.height);
        mContext.font = '26px sans-serif';
        mContext.fillStyle = '#88F';
        mContext.fillText('ERROR: ' + error, 15, 25);
    }

    function DrawFrame(sequence, image, draw)
    {
        mDrawString.innerHTML = 'FRAME ' + sequence + ", " + draw;

        mContext.fillStyle = '#000';
        mContext.fillRect(0, 0, mCanvas.width, mCanvas.height);

        mContext.drawImage(image, 0, 0);

        mContext.font = '26px sans-serif';
        mContext.fillStyle = '#88F';
        mContext.fillText('Frame: ' + mSequence, 15, 25);
    }

    function LoadFrame()
    {
        var sequence = mSequence;
        mSequence = mSequence + 1;

        var request = new WrvRequest(mSessionId, sequence);
        request.OnDone = function ()
        {
            if (request.Error != null)
            {
                console.log("Error loading frame " + request.Sequence + ": " + request.Error);
                ShowError("Loading frame " + sequence + ", " + request.Error);
                return;
            }
            DrawFrame(sequence, this.Image, this.Draw);
            LoadFrame();
        };
        request.Start(mSessionId, sequence);
        console.log("LoadFrame EXIT: " + sequence);
    }

    function StartSession()
    {
        var xhttp = new XMLHttpRequest();
        xhttp.onreadystatechange = function ()
        {
            if (this.readyState != 4)
                return;
            if (this.status != 200)
            {
                ShowError("Starting session");
                return;
            }
            mSessionId = JSON.parse(xhttp.responseText).sid;
            LoadFrame();
        };
        xhttp.open("GET", "index.wrv?query=startsession&rid=" + (new Date()).getMilliseconds(), true);
        xhttp.send();
    }

    THIS.Start = function ()
    {
        InitializeCanvas();
        StartSession();
    }
}

