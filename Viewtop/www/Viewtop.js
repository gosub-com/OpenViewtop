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
    var mGetSequence = 1;
    var mPutSequence = 1;
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

    var mGetsOutstanding = 0;
    var mGetSendTime = 0;

    var mPutsOutstanding = 0;
    var mPutSendTime = 0;

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

        canvas.onmousemove = OnMouseMove;
        canvas.onmousedown = OnMouseDown;
        canvas.onmouseup = OnMouseUp;
        canvas.onmousewheel = OnMouseWheel;
        canvas.onkeypress = OnKeyPress;
        canvas.oncontextmenu = function () { return false; }
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

    function StopInternal()
    {
        mRunning = false;
        canvas.onmousemove = function () { };
        canvas.onmousedown = function () { };
        canvas.onmouseup = function () { };
        canvas.onmousewheel = function () { };
        canvas.onkeydown = function () { };
        canvas.oncontextmenu = function () { return true; }
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

    // Called when the user presses a key
    function OnKeyPress(e)
    {
        var keyEvent = {};
        keyEvent.Time = Date.now();
        keyEvent.Event = "keypress";
        keyEvent.KeyCode = e.keyCode;
        keyEvent.KeyChar = e.charCode;
        keyEvent.KeyShift = e.shiftKey;
        keyEvent.KeyCtrl = e.ctrlKey;
        keyEvent.KeyAlt = e.altKey;
        mKeyAndMouseEvents.push(keyEvent);
        preventDefault(e);
    }

    // Prevent the canvas default action (e.g. no mouse wheel, etc.)
    function preventDefault(e)
    {
        if (e.preventDefault)
        {
            e.preventDefault();
            return;
        }
        e.returnValue = false;
    }

    function StartSession()
    {
        var xhttp = new XMLHttpRequest();
        xhttp.open("GET", "index.ovt?query=startsession&rid=" + Date.now() + "&username=" + mUsername, true);
        xhttp.send();
        xhttp.onreadystatechange = function ()
        {
            if (!HttpRequestDone(this, "Start session request failed"))
                return;
            var sessionInfo = JSON.parse(xhttp.responseText);
            mSessionId = sessionInfo.sid;
            Login(sessionInfo.salt, sessionInfo.challenge);
        };
    }

    function Login(salt, challenge)
    {
        var xhttp = new XMLHttpRequest();
        xhttp.open("GET", "index.ovt?query=login&sid=" + mSessionId
            + "&username=" + mUsername
            + "&hash=" + Sha256.hash(challenge + Sha256.hash(salt + mPassword).toUpperCase()).toUpperCase());
        xhttp.send();
        xhttp.onreadystatechange = function ()
        {
            if (!HttpRequestDone(this, "Login request failed"))
                return;
            var loginInfo = JSON.parse(xhttp.responseText);
            if (!loginInfo.pass)
            {
                ShowError("Invalid user name or password");
                return;
            }
            EventsLoop();
        };
    }

    // Main event processing loop, call ProcessEvents every 33 milliseconds
    function EventsLoop()
    {
        if (!mRunning)
            return;
        setTimeout(function () { EventsLoop(); }, 33);
        ProcessEvents();
    }

    // Process all events.  Called every 33 milliseconds or whenever
    // something is done processing (draw frame, keyboard, etc.)
    function ProcessEvents()
    {
        if (!mRunning)
            return;

        // Request frame
        if (mGetsOutstanding == 0)
            LoadFrame();

        // Request next frame before receiving previous frame 
        // (i.e. double buffer) to reduce the round trip time latency.  
        // TBD: Use round trip timer instead of hardcoded value
        var ROUND_TRIP_TIME = 80;
        if (mGetsOutstanding == 1 && Date.now() - mGetSendTime > ROUND_TRIP_TIME / 2)
            LoadFrame();

        // Send mouse and keyboard events
        if (mPutsOutstanding == 0 && mKeyAndMouseEvents.length != 0)
            SendEvents();
    }

    function SendEvents()
    {
        console.log(mKeyAndMouseEvents);

        var jsonObject = { Events: mKeyAndMouseEvents};
        mKeyAndMouseEvents = [];

        console.log(jsonObject);


        var sequence = mPutSequence;
        mPutSequence = mPutSequence + 1;

        mPutSendTime = Date.now();
        mPutsOutstanding++;
        var xhttp = new XMLHttpRequest();
        xhttp.open("PUT", "index.ovt?query=events&sid=" + mSessionId
            + "&seq=" + sequence, true);
        xhttp.send(JSON.stringify(jsonObject));
        xhttp.onreadystatechange = function()
        {
            if (!HttpRequestDone(this, "Send events failed"))
                return;
            mPutsOutstanding--;
            ProcessEvents();
        }
    }


    function LoadFrame()
    {
        var sequence = mGetSequence;
        mGetSequence = mGetSequence + 1;

        // Start downloading the draw buffer
        mGetSendTime = Date.now();
        mGetsOutstanding++;
        var xhttp = new XMLHttpRequest();
        xhttp.open("GET", "index.ovt?query=draw&sid=" + mSessionId
            + "&seq=" + sequence
            + "&t=" + mGetSendTime
            + "&x=" + mMouseX
            + "&y=" + mMouseY
            + "&" + THIS.DrawOptions, true);
        xhttp.send();
        xhttp.onreadystatechange = function ()
        {
            if (!HttpRequestDone(this, "Frame request failed"))
                return;
            mGetsOutstanding--;

            // NOTE: This might be early since the images have to be loaded
            //       by the browser before drawing.  But since that's all
            //       done locally, it seems unlikely that a new frame could 
            //       arrive remotely and be drawn out of sequence
            ProcessEvents();

            var startTime = Date.now();
            var drawBuffer = JSON.parse(xhttp.responseText);
            drawBuffer.DrawStartTime = startTime;

            LoadImagesThenDraw(drawBuffer);
        };        
    }

    // Load all the images in parallel, then draw them
    function LoadImagesThenDraw(drawBuffer)
    {        
        // Load all images, then issue draw after all of them have loaded
        var frames = drawBuffer.Frames;
        var totalImagesLoaded = 0;
        for (var i = 0; i < frames.length; i++)
        {
            var image = new Image();
            image.onload = ScopeServer(i, image, function(i, image)
            {
                frames[i].LoadedImage = image;

                // The the images after they are all loaded
                if (++totalImagesLoaded == frames.length)
                    DrawImages(drawBuffer);
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
        stats.DrawTime = Date.now() - drawBuffer.DrawStartTime;
        stats.FPS = mFps;
        var statsStr = "";
        for (var key in stats)
            if (stats.hasOwnProperty(key))
                statsStr += key + ": " + stats[key] + "<br>";
        mDrawString.innerHTML = statsStr;
    }

    function HttpRequestDone(request, errorMessage)
    {
        if (!mRunning)
            return false;
        if (request.readyState != 4)
            return false;
        if (request.status != 200)
        {
            ShowError(errorMessage);
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
                break;

            case 'Y': // Target height
                targetHeight = n;
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

}
