//
// Copyright (C) 2016 by Jeremy Spiller, all rights reserved
//

"use strict"

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
    var mSessionChallenge = "";
    var mSequence = 1;
    var mRunning = false;
    var mUsername = "";
    var mPassword = "";

    // Set draw options (each separated by '&')
    THIS.DrawOptions = "";

    // Start the session
    THIS.Start = function (username, password)
    {
        mUsername = username;
        mPassword = password;
        mRunning = true;
        InitializeCanvas();
        StartSession(username, password);
    }

    // Stop the session
    THIS.Stop = function()
    {
        mRunning = false;
    }

    // Called when the session ends because of an error
    THIS.ErrorCallback = function(errorMessage)
    {
    }

    function StartSession()
    {
        var xhttp = new XMLHttpRequest();
        xhttp.open("GET", "index.ovt?query=startsession&rid=" + (new Date()).getMilliseconds(), true);
        xhttp.send();
        xhttp.onreadystatechange = function ()
        {
            if (!HttpRequestDone(this, "Start session request failed"))
                return;
            var sessionInfo = JSON.parse(xhttp.responseText);
            mSessionId = sessionInfo.sid;
            mSessionChallenge = sessionInfo.challenge;
            Login();
        };
    }

    function Login()
    {
        var xhttp = new XMLHttpRequest();
        xhttp.open("GET", "index.ovt?query=login&sid=" + mSessionId
            + "&username=" + mUsername + "&password=" + mPassword, true);
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
            LoadFrame();
        };
    }

    function LoadFrame()
    {
        var sequence = mSequence;
        mSequence = mSequence + 1;

        // Start downloading the draw buffer

        var xhttp = new XMLHttpRequest();
        xhttp.open("GET", "index.ovt?query=draw&sid=" + mSessionId + "&seq=" + sequence + "&" + THIS.DrawOptions, true);
        xhttp.send();
        xhttp.onreadystatechange = function ()
        {
            if (!HttpRequestDone(this, "Frame request failed"))
                return;

            var startTime = new Date().getMilliseconds();
            var drawBuffer = JSON.parse(xhttp.responseText);
            drawBuffer.DrawStartTime = startTime;

            LoadImagesThenDraw(drawBuffer);
        };        
    }

    // Load all the images in parallel, then draw them
    function LoadImagesThenDraw(drawBuffer)
    {
        var frames = drawBuffer.Frames;
        
        if (frames.length == 0)
        {
            DrawImagesThenLoadNextFrame(drawBuffer);
            return;
        }

        // Load images, issue draw after all of them have loaded
        var totalImagesLoaded = 0;
        for (var i = 0; i < frames.length; i++)
        {
            var image = new Image();
            image.onload = ScopeServer(i, image, function(i, image)
            {
                frames[i].LoadedImage = image;

                // The the images after they are all loaded
                if (++totalImagesLoaded == frames.length)
                    DrawImagesThenLoadNextFrame(drawBuffer);
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

    function DrawImagesThenLoadNextFrame(drawBuffer)
    {
        // We can issue the load before drawing since it won't run until this exits
        LoadFrame();

        // Draw frames
        var frames = drawBuffer.Frames;
        for (var i = 0; i < frames.length; i++)
            DrawFrame(frames[i].LoadedImage, frames[i].Draw);

        // Show stats
        var stats = drawBuffer.Stats;
        stats.DrawTime = new Date().getMilliseconds() - drawBuffer.DrawStartTime;
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
        mRunning = false;
    }
    function ShowMessage(errorMessage)
    {
        mContext.fillStyle = '#000';
        mContext.fillRect(0, 0, mCanvas.width, mCanvas.height);
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

