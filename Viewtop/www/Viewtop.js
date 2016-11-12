//
// Copyright (C) 2016 by Jeremy Spiller, all rights reserved
//

"use strict"

//
// Request a draw buffer and image.
// Call Start to start the request, and use OnDone to retrieve the result.
//
function OvtRequest()
{
    var THIS = this;
    THIS.SessionId = null;
    THIS.Sequence = null;
    THIS.Image = null;
    THIS.Draw = null;
    THIS.Error = null;
    THIS.DrawOptions = "";

    // Called when Image and Draw have loaded (Error is null if everything is OK)
    THIS.OnDone = function () { }

    // Start downloading the sequence (OnDone is called when done or there is an error)
    THIS.Start = function (sessionId, sequence, drawOptions)
    {
        THIS.SessionId = sessionId;
        THIS.Sequence = sequence;
        THIS.DrawOptions = drawOptions;

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
        image.src = "index.ovt?query=image&sid=" + THIS.SessionId + "&seq=" + THIS.Sequence;

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
        xhttp.open("GET", "index.ovt?query=draw&sid=" + THIS.SessionId + "&seq=" + THIS.Sequence + "&" + THIS.DrawOptions, true);
        xhttp.send();
    }

    // Set an eror, call OnDone if it hasn't already been called
    function SetError(error)
    {
        console.log("OvtRequest Error: " + error);

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
function Viewtop(drawString, canvas)
{
    var THIS = this;
    var mDrawString = drawString;
    var mCanvas = canvas;
    var mContext = canvas.getContext('2d');
    var mSessionId = 0;
    var mSequence = 1;

    // Set draw options (each separated by '&')
    THIS.DrawOptions = "";

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

    function DrawFrame(sequence, source, drawBuffer)
    {
        var startTime = new Date().getMilliseconds();

        var draw = drawBuffer.Draw;
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

        var stats = drawBuffer.Stats;
        stats.DrawTime = new Date().getMilliseconds() - startTime;
        var statsStr = "";
        for (var key in stats) {
            if (stats.hasOwnProperty(key)) {
                statsStr += key + ": " + stats[key] + "<br>";
            }
        }
        mDrawString.innerHTML = statsStr;

    }

    function LoadFrame()
    {
        var sequence = mSequence;
        mSequence = mSequence + 1;

        var request = new OvtRequest();
        request.OnDone = function ()
        {
            if (request.Error != null)
            {
                console.log("Error loading frame " + request.Sequence + ": " + request.Error);
                ShowError("Loading frame " + sequence + ", " + request.Error);
                return;
            }
            DrawFrame(sequence, this.Image, JSON.parse(this.Draw));
            LoadFrame();
        };
        request.Start(mSessionId, sequence, THIS.DrawOptions);
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
        xhttp.open("GET", "index.ovt?query=startsession&rid=" + (new Date()).getMilliseconds(), true);
        xhttp.send();
    }

    THIS.Start = function ()
    {
        InitializeCanvas();
        StartSession();
    }
}

