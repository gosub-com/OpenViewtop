//
// Copyright (C) 2016 by Jeremy Spiller, all rights reserved
//

"use strict"

// NOTE: Include "Sha256.js"

//
// Main remote viewer class, used to continuosly update the canvas.
//
function Viewtop(drawString, canvas)
{
    var THIS = this;
    var mDrawString = drawString;
    var mCanvas = canvas;
    var mContext = canvas.getContext('2d');
    var mSessionId = 0;
    var mRunning = false;
    var mUsername = "";
    var mPassword = "";

    var mMouseMoveTime = 0;
    var mMouseX = 0;
    var mMouseY = 0;
    var mKeyAndMouseEvents = [];

    var mFps = 0;
    var mFpsCounter = 0;
    var mFpsTime = Date.now();

    var mGetSequence = 1;
    var mGetSendTime = 0;

    var mDrawSequence = 1;
    var mDrawQueue = {};

    var mPutSequence = 1;
    var mPutsOutstanding = 0;
    var mPutSendTime = 0;

    // Round trip time
    var mRtt = 400;
    var mRttSkipsInARow = 0;
    var RTT_MAX = 1200;
    var RTT_MIN = 25;
    var RTT_COEFFICIENT = 0.2;
    var RTT_TRIGGER_RATIO = 0.5;  // Trigger at middle of RTT
    var RTT_TRIGGER_MIN_MS = 40;  // Trigger a bit before middle of RTT

    function SetRtt(rtt)
    {
        // Ignore net lag hiccups, which usually come in pairs because of double buffering
        if (rtt > 1.6*mRtt && mRttSkipsInARow++ < 2)
            return;
        mRttSkipsInARow = 0;

        // Butterworth filter
        mRtt = Math.min(Math.max(rtt, RTT_MIN), RTT_MAX) * RTT_COEFFICIENT + (1 - RTT_COEFFICIENT) * mRtt;
    }


    // Set draw options (each separated by '&')
    this.DrawOptions = "";

    // Start the session
    this.Start = function (username, password)
    {
        mUsername = username;
        mPassword = password;
        mRunning = true;
        InitializeCanvas();
        StartSession(username, password);

        mCanvas.onmousemove = OnMouseMove;
        mCanvas.onmousedown = OnMouseDown;
        mCanvas.onmouseup = OnMouseUp;
        mCanvas.onmousewheel = OnMouseWheel;
        mCanvas.onkeydown = OnKeyDown;
        mCanvas.onkeypress = OnKeyPress;
        mCanvas.onkeyup = OnKeyUp;
        mCanvas.oncontextmenu = function () { return false; }
    }

    // Stop the session
    this.Stop = function()
    {
        StopInternal();
    }

    // Called when the session ends because of an error
    this.ErrorCallback = function(errorMessage)
    {
    }

    this.ClipboardButton = {};
    this.ComputerNameLabel = {};

    function StopInternal()
    {
        mRunning = false;
        mCanvas.onmousemove = function () { };
        mCanvas.onmousedown = function () { };
        mCanvas.onmouseup = function () { };
        mCanvas.onmousewheel = function () { };
        mCanvas.onkeydown = function () { };
        mCanvas.onkeypress = function () { };
        mCanvas.onkeyup = function () { };
        mCanvas.oncontextmenu = function () { return true; }
    }

    // Called when the user clicks the canvas
    function OnMouseDown(e)
    {
        mKeyAndMouseEvents.push(GetMouseEvent("mousedown", e));
    }

    // Called when the user releases the mouse button
    function OnMouseUp(e)
    {
        mKeyAndMouseEvents.push(GetMouseEvent("mouseup", e));
    }

    // Called when the user scrolls the mouse wheel
    function OnMouseWheel(e)
    {
        var mouseEvent = GetMouseEvent("mousewheel", e);
        mouseEvent.Delta = Math.max(-1, Math.min(1, (e.wheelDelta || -e.detail)));
        mKeyAndMouseEvents.push(mouseEvent);
        preventDefault(e);
        return false;
    }

    function GetMouseEvent(eventName, e)
    {
        OnMouseMove(e);
        var mouseEvent = {};
        mouseEvent.Event = eventName;
        mouseEvent.Which = e.which;
        mouseEvent.Time = mMouseMoveTime;
        mouseEvent.X = mMouseX;
        mouseEvent.Y = mMouseY;
        return mouseEvent;
    }

    // Called when user moves the mouse over the canvas
    function OnMouseMove(e)
    {
        // The mouse event will be sent with the next frame of after some
        // time if the frame is delayed
        var r = canvas.getBoundingClientRect();
        mMouseX = Math.round(e.clientX - r.left);
        mMouseY = Math.round(e.clientY - r.top);
        mMouseMoveTime = Date.now();
    }    

    function OnKeyDown(e)
    {
        OnKey(e, "keydown");
    }
    function OnKeyPress(e)
    {
        preventDefault(e);
    }
    function OnKeyUp(e)
    {
        OnKey(e, "keyup");
    }
    // Called when a keyboard event is recorded
    function OnKey(e, eventName)
    {
        var keyEvent = {};
        keyEvent.Time = Date.now();
        keyEvent.Event = eventName;
        keyEvent.KeyCode = e.keyCode;
        keyEvent.KeyShift = e.shiftKey;
        keyEvent.KeyCtrl = e.ctrlKey;
        keyEvent.KeyAlt = e.altKey;
        mKeyAndMouseEvents.push(keyEvent);
        preventDefault(e);
    }

    // Prevent the canvas default action (e.g. no mouse wheel, etc.)
    function preventDefault(e)
    {
        e.returnValue = false;
        e.preventDefault();
        e.stopPropagation();
    }

    function StartSession()
    {
        HttpGet("index.ovt?query=startsession&rid=" + Date.now() + "&username=" + mUsername,
        function ()
        {
            if (!HttpDone(this, "Start session request failed"))
                return;
            var sessionInfo = JSON.parse(this.responseText);
            mSessionId = sessionInfo.sid;
            Login(sessionInfo.salt, sessionInfo.challenge);
        });
    }

    function Login(salt, challenge)
    {
        HttpGet("index.ovt?query=login&sid=" + mSessionId
                + "&username=" + mUsername
                + "&hash=" + Sha256.hash(challenge + Sha256.hash(salt + mPassword).toUpperCase()).toUpperCase(),
        function ()
        {
            if (!HttpDone(this, "Login request failed"))
                return;
            var loginInfo = JSON.parse(this.responseText);
            if (!loginInfo.LoggedIn)
            {
                ShowError("Invalid user name or password");
                return;
            }
            THIS.ComputerNameLabel.innerHTML = loginInfo.ComputerName;
            EventsLoop();
        });
    }

    // Main event processing loop, call ProcessEvents every 23 milliseconds
    function EventsLoop()
    {
        if (!mRunning)
            return;
        setTimeout(function () { EventsLoop(); }, 23);
        ProcessEvents();
    }

    // Process all events.  Called every 23 milliseconds or whenever
    // something is done processing (draw frame, keyboard, etc.)
    function ProcessEvents()
    {
        if (!mRunning)
            return;

        try
        {
            // Draw frames from the queue
            var drawBuffer = mDrawQueue[mDrawSequence];
            while (drawBuffer)
            {
                delete mDrawQueue[mDrawSequence];
                mDrawSequence++;
                ProcessData(drawBuffer);
                drawBuffer = mDrawQueue[mDrawSequence];
            }

            // Request frame
            var getsOutstanding = mGetSequence - mDrawSequence;
            if (getsOutstanding == 0)
                LoadFrame();

            // Request next frame before receiving previous frame 
            // (i.e. double buffer) to reduce the round trip time latency.  
            if (getsOutstanding == 1 && Date.now() - mGetSendTime > mRtt * RTT_TRIGGER_RATIO - RTT_TRIGGER_MIN_MS)
                LoadFrame();

            // Send mouse and keyboard events
            if (mPutsOutstanding == 0 && mKeyAndMouseEvents.length != 0)
                SendEvents();
        }
        catch (e)
        {
            mRunning = false;
            ShowError(e.stack);
        }
    }

    function SendEvents()
    {
        var jsonObject = { Events: mKeyAndMouseEvents};
        mKeyAndMouseEvents = [];

        var sequence = mPutSequence;
        mPutSequence = mPutSequence + 1;

        mPutSendTime = Date.now();
        mPutsOutstanding++;
        HttpPut("index.ovt?query=events&sid=" + mSessionId + "&seq=" + sequence,
                JSON.stringify(jsonObject),
        function ()
        { 
            if (!HttpDone(this, "Send events failed"))
                return;
            mPutsOutstanding--;
            ProcessEvents();
        });
    }

    function LoadFrame()
    {
        var sequence = mGetSequence;
        mGetSequence = mGetSequence + 1;

        // Find canvas size
        var WINDOW_BORDER_HEIGHT = 40; // Document margin, scroll bar, menu bar at top, etc.
        var fullscreen = document.fullscreenElement || document.mozFullScreenElement || document.webkitFullscreenElement;
        var width = fullscreen ? window.innerWidth : document.body.clientWidth;
        var height = fullscreen ? window.innerHeight : (window.innerHeight - WINDOW_BORDER_HEIGHT);

        // Start downloading the draw buffer
        var sendTime = Date.now();
        mGetSendTime = sendTime;
        HttpGet("index.ovt?query=draw&sid=" + mSessionId
                        + "&seq=" + sequence
                        + "&t=" + mGetSendTime
                        + "&x=" + mMouseX
                        + "&y=" + mMouseY
                        + "&maxwidth=" + width
                        + "&maxheight=" + height
                        + "&" + THIS.DrawOptions,
        function ()
        {
            if (!HttpDone(this, "Frame request failed"))
                return;
            SetRtt(Date.now() - sendTime);
            LoadImagesThenQueue(JSON.parse(this.responseText), sequence);
        });
    }

    // Load all the images in parallel, then queue the frame to be drawn in the order received
    function LoadImagesThenQueue(drawBuffer, sequence)
    {
        var frames = drawBuffer.Frames;
        var totalImagesLoaded = 0;
        for (var i = 0; i < frames.length; i++)
        {
            // Ask browser to load each image
            var image = new Image();
            image.onload = ScopeServer(i, image, function(i, image)
            {
                // Check each loaded image to see if its the last one
                frames[i].LoadedImage = image;
                if (++totalImagesLoaded == frames.length)
                {
                    // Queue the image
                    mDrawQueue[sequence] = drawBuffer;
                    ProcessEvents();
                }
            });
            image.onerror = function ()
            {
                ShowError("Error decoding image");
            };
            image.src = frames[i].Image;
        }
    }

    // Scope server to capture two local parameters
    // See http://www.howtocreate.co.uk/referencedvariables.html 
    // for details on why this is necessary
    function ScopeServer(param1, param2, f)
    {
        return function () { return f(param1, param2); }
    }

    function ProcessData(drawBuffer)
    {
        SetClipboard(drawBuffer);
        DrawImages(drawBuffer);
    }


    var mClipChangedTime = 0;
    function SetClipboard(drawBuffer)
    {
        var clip = THIS.ClipboardButton;

        if (drawBuffer.Clip.Type == "Text")
        {
            // Download text
            clip.style = "";
            clip.title = "Download text";
            clip.setAttribute('href', 'text');
            clip.setAttribute('download', 'text');
            clip.onclick = function ()
            {
                // Download text, then show message to allow user to copy using CTRL-C
                HttpGet("index.ovt?query=clip&sid=" + mSessionId + "&rid=" + mClipChangedTime,
                function ()
                {
                    if (!HttpDone(this, "Copy clipboard text failed"))
                        return;
                    window.prompt("Copy to clipboard: Ctrl+C, Enter", this.responseText);
                });
                return false;
            };
        }
        else if (drawBuffer.Clip.Type == "File")
        {
            // Download file
            clip.style = "";
            clip.title = "DOWNLOAD FILE: '" + drawBuffer.Clip.FileName + "'"
                + (drawBuffer.Clip.FileCount == 1 ? "" : ", (" + drawBuffer.Clip.FileCount + " files)");
            clip.setAttribute('href', "index.ovt?query=clip&sid=" + mSessionId + "&rid=" + mClipChangedTime);
            clip.setAttribute('download', drawBuffer.Clip.FileName);
            clip.onclick = function () { };
        }
        else if (drawBuffer.Clip.Type == "")
        {
            // Disabled
            clip.style = "color:#606060;background:#D0D0D0;border: 1px solid #ADADAD";
            clip.setAttribute('href', '');
            clip.setAttribute('download', '');
            clip.onclick = function () { return false; };
        }

        // Flash when new data arrives
        if (drawBuffer.Clip.Changed)
            mClipChangedTime = Date.now();
        if (Date.now() - mClipChangedTime < 250)
            clip.style = "background:#FFF080;color:#000060;border: 2px solid #B09820";
    }

    function DrawImages(drawBuffer)
    {
        mFpsCounter++;
        if (mFpsTime != Math.round(Date.now()/1000))
        {
            mFpsTime = Math.round(Date.now()/1000);
            mFps = mFpsCounter;
            mFpsCounter = 0;
        }

        // Draw frames
        var frames = drawBuffer.Frames;
        for (var i = 0; i < frames.length; i++)
            DrawFrame(frames[i].LoadedImage, frames[i].Draw);

        // Show stats
        var stats = drawBuffer.Stats;
        stats.FPS = mFps;
        var statsStr = "";
        for (var key in stats)
            if (stats.hasOwnProperty(key))
                statsStr += key + ": " + stats[key] + "<br>";
        mDrawString.innerHTML = statsStr;
    }

    function HttpPut(url, data, onreadystatechangefunction)
    {
        var xhttp = new XMLHttpRequest();
        xhttp.onreadystatechange = onreadystatechangefunction;
        xhttp.open("PUT", url, true);
        xhttp.send(data);
    }

    function HttpGet(url, onreadystatechangefunction)
    {
        var xhttp = new XMLHttpRequest();
        xhttp.onreadystatechange = onreadystatechangefunction;
        xhttp.open("GET", url, true);
        xhttp.send();
    }

    function HttpDone(request, errorMessage)
    {
        if (!mRunning)
            return false;
        if (request.readyState != 4)
            return false;
        if (request.status != 200)
        {
            var moreInfo = "";
            try { moreInfo = ", " + JSON.parse(request.responseText).FAIL; }
            catch (e) { }
            ShowError(errorMessage + moreInfo);
            return false;
        }
        return true;
    }

    function InitializeCanvas()
    {
        mDrawString.innerHTML = "Starting...";
        mContext.fillStyle = '#228';
        mContext.fillRect(0, 0, mCanvas.width, mCanvas.height);
        mContext.fillStyle = '#fff';
        mContext.font = '60px sans-serif';
        mContext.fillText('Starting...', 10, mCanvas.height / 2 - 15);
    }

    function ShowError(errorMessage)
    {
        mDrawString.innerHTML = 'ERROR: ' + errorMessage;
        mContext.fillStyle = '#000';
        mContext.fillRect(0, 0, mCanvas.width, mCanvas.height);
        mContext.font = '26px sans-serif';
        mContext.fillStyle = '#88F';
        mContext.fillText('ERROR: ' + errorMessage, 15, 25);
        THIS.ErrorCallback(errorMessage);
        StopInternal();
    }
    function ShowMessage(errorMessage)
    {
        mContext.fillStyle = '#000';
        mContext.fillRect(0, 0, mCanvas.width,25);
        mContext.font = '26px sans-serif';
        mContext.fillStyle = '#88F';
        mContext.fillText('MSG: ' + errorMessage, 15, 25);
    }

    // Draw an image using a draw string, which is a list of single character
    // commands optionally followed by a number.  Example, when draw is:
    //      "x1680y1050C6930"
    // It sets target screen size to 1680x1050 and copies the entire source
    function DrawFrame(source, draw)
    {
        mContext.fillStyle = '#000';

        var targetWidth = 0;
        var targetHeight = 0;
        var blockSize = 16;
        var blocksSourceX = 1;
        var blocksTargetX = 1;
        var colors = new Array();
        var dupX = 0;
        var dupY = 0;
        var si = 0; // Source index
        var ti = 0; // Target index
        var i = 0;
        var command = '';
        var n = 0;
        while (i < draw.length)
        {
            // Parse the command and number
            command = draw[i++];
            n = 0;
            while (i < draw.length)
            {
                var code = draw.charCodeAt(i);
                if (code < 48 || code > 57)
                    break;
                n = n*10 + (code-48);
                i++;
            }
            switch (command)
            {
            case 'X': // Target width
                targetWidth = n;
                blocksSourceX = Math.floor((source.width + blockSize - 1) / blockSize);
                blocksTargetX = Math.floor((targetWidth + blockSize - 1) / blockSize);
                SetCanvasSize(targetWidth, mCanvas.height);
                break;

            case 'Y': // Target height
                targetHeight = n;
                SetCanvasSize(mCanvas.width, targetHeight);
                break;

            case 'B': // Block size                    
                blockSize = n;
                blocksSourceX = Math.floor((source.width + blockSize - 1) / blockSize);
                blocksTargetX = Math.floor((targetWidth + blockSize - 1) / blockSize);
                break;

            case '!':
                mContext.drawImage(source, 0, 0);
                break;

            case 'K': // Skip
                if (n == 0)
                    n = 1;
                ti = ti + n;
                break;

            case 'C': // Copy
                if (n == 0)
                    n = 1;

                while (n > 0)
                {
                    var targetX = Math.floor(ti % blocksTargetX) * blockSize;
                    var targetY = Math.floor(ti / blocksTargetX) * blockSize;
                    var sourceX = Math.floor(si % blocksSourceX) * blockSize;
                    var sourceY = Math.floor(si / blocksSourceX) * blockSize;

                    // Calculate blocks to copy (don't go past end of line)
                    var btoc = Math.min(n, blocksTargetX - Math.floor(ti % blocksTargetX),
                                            blocksSourceX - Math.floor(si % blocksSourceX));
                    n -= btoc;
                    ti += btoc;
                    si += btoc;

                    mContext.drawImage(source, sourceX, sourceY, btoc*blockSize, blockSize, targetX, targetY, btoc*blockSize, blockSize);
                }
                break;

            case 's': // Solid color
                var colorHex = '00000' + n.toString(16).toUpperCase();
                var color = '#' + colorHex.substr(colorHex.length - 6, 6);
                mContext.fillStyle = color;
                break;

            case 'S': // Solid
                if (n == 0)
                    n = 1;

                while (n > 0)
                {
                    var targetX = Math.floor(ti % blocksTargetX) * blockSize;
                    var targetY = Math.floor(ti / blocksTargetX) * blockSize;

                    // Calculate blocks to copy (don't go past end of line)
                    var btoc = Math.min(n, blocksTargetX - Math.floor(ti % blocksTargetX));
                    n -= btoc;
                    ti += btoc;

                    mContext.fillRect(targetX, targetY, btoc*blockSize, blockSize);
                }
                break;

            case 'd': // Duplicate position
                dupX = Math.floor(n % blocksSourceX) * blockSize;
                dupY = Math.floor(n / blocksSourceX) * blockSize;
                break;

            case 'D': // Duplicate                    
                if (n == 0)
                    n = 1;
                while (n > 0)
                {
                    var targetX = Math.floor(ti % blocksTargetX) * blockSize;
                    var targetY = Math.floor(ti / blocksTargetX) * blockSize;
                    n--;
                    ti++;
                    mContext.drawImage(source, dupX, dupY, blockSize, blockSize, targetX, targetY, blockSize, blockSize);
                }
                break;
            }
        }
    }

    function SetCanvasSize(width, height)
    {
        // NOTE: Firefox and Edge scale the canvas to screen size, which destroys
        //       the mouse scaling and makes it look ugly when enlarged.
        //       Chrome centers the canvas nicely, so don't change anything
        var fullscreenButNotChrome = document.fullscreenElement || document.mozFullScreenElement;
        if (fullscreenButNotChrome)
        {
            // TBD: Should draw the image in the center of the canvas
            // TBD: This doesn't scale correctly in Edge
            width = window.innerWidth;
            height = window.innerHeight;
        }

        if (mCanvas.width != width)
        {
            mCanvas.width = width;
            mCanvas.style.width = width + "px";
        }
        if (mCanvas.height != height)
        {
            mCanvas.height = height;
            mCanvas.style.height = height + "px";
        }
    }

}
