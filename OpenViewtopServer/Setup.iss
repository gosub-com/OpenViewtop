[Setup]
AppName=Open Viewtop
AppVersion=1.0.0
OutputDir=.
OutputBaseFilename=SetupOpenViewtop
UsePreviousAppDir=false
UsePreviousGroup=false
DefaultDirName={pf64}\Gosub\Open Viewtop
DefaultGroupName=Open Viewtop
AppPublisher=Gosub Software
UninstallDisplayName=Open Viewtop
UninstallDisplayIcon={app}\OpenViewtopServer.exe
LicenseFile=License.txt
PrivilegesRequired=admin 

[Files]
Source: "OpenViewtopServer.exe"; DestDir: "{app}"; flags:ignoreversion
Source: "Gosub.OpenViewtop.dll"; DestDir: "{app}"; flags:ignoreversion
Source: "www\*"; DestDir: "{app}\www"; flags:recursesubdirs ignoreversion
Source: "Newtonsoft.Json.dll"; DestDir: "{app}"; flags:ignoreversion
Source: "Mono.Security.dll"; DestDir: "{app}"; flags:ignoreversion

[Icons]
Name: "{group}\Open Viewtop"; Filename: "{app}\OpenViewtopServer.exe"
Name: "{group}\Uninstall Open Viewtop"; Filename: "{uninstallexe}"

[Run]
FileName: "{app}\OpenViewtopServer.exe"; Flags: Postinstall

[Code]

const
	OPEN_VIEWTOP_SERVER_EXE = 'OpenViewtopServer.exe';
	OPEN_VIEWTOP_SERVICE_NAME = 'OpenViewtopService';

// **************************************************************************************
// The following code is for adding rules to the firewall
// http://www.vincenzo.net/isxkb/index.php?title=Adding_a_rule_to_the_Windows_firewall
// **************************************************************************************

const
  NET_FW_SCOPE_ALL = 0;
  NET_FW_IP_VERSION_ANY = 2;
  NET_FW_ACTION_ALLOW = 1;
  NET_FW_PROTOCOL_TCP = 6;
  NET_FW_PROTOCOL_UDP = 17;


procedure SetFirewallExceptionXP(AppName,FileName:string);
var
  FirewallObject: Variant;
  FirewallManager: Variant;
  FirewallProfile: Variant;
begin
  try
    FirewallObject := CreateOleObject('HNetCfg.FwAuthorizedApplication');
    FirewallObject.ProcessImageFileName := FileName;
    FirewallObject.Name := AppName;
    FirewallObject.Scope := NET_FW_SCOPE_ALL;
    FirewallObject.IpVersion := NET_FW_IP_VERSION_ANY;
    FirewallObject.Enabled := True;
    FirewallManager := CreateOleObject('HNetCfg.FwMgr');
    FirewallProfile := FirewallManager.LocalPolicy.CurrentProfile;
    FirewallProfile.AuthorizedApplications.Add(FirewallObject);
  except
  end;
end;

procedure SetFirewallExceptionVista(AppName,FileName:string);
var
  firewallRule: Variant;
  firewallPolicy: Variant;
begin
  try
    firewallRule := CreateOleObject('HNetCfg.FWRule');
    firewallRule.Action := NET_FW_ACTION_ALLOW;
    firewallRule.Description := AppName;
    firewallRule.ApplicationName := FileName;
    firewallRule.Enabled := True;
    firewallRule.InterfaceTypes := 'All';
    firewallRule.Name := AppName;

    firewallPolicy := CreateOleObject('HNetCfg.FwPolicy2');
    firewallPolicy.Rules.Add(firewallRule);
  except
  end;
end;

procedure SetFirewallException(AppName,FileName:string);
var
  WindVer: TWindowsVersion;
begin
  try
    GetWindowsVersionEx(WindVer);
    if WindVer.NTPlatform and (WindVer.Major >= 6) then
      SetFirewallExceptionVista(AppName,FileName)
    else
      SetFirewallExceptionXP(AppName,FileName);
  except
  end;
end;

procedure RemoveFirewallException( FileName:string );
var
  FirewallManager: Variant;
  FirewallProfile: Variant;
begin
  try
    FirewallManager := CreateOleObject('HNetCfg.FwMgr');
    FirewallProfile := FirewallManager.LocalPolicy.CurrentProfile;
    FireWallProfile.AuthorizedApplications.Remove(FileName);
  except
  end;
end;


procedure SetFirewallPortException(AppName: string; Protocol, Port: integer);
var
  FirewallObject: Variant;
  FirewallManager: Variant;
  FirewallProfile: Variant;
begin
  try
    FirewallObject := CreateOleObject('HNetCfg.FwOpenPort');
    FirewallObject.Name := AppName;
    FirewallObject.Scope := NET_FW_SCOPE_ALL;
    FirewallObject.IpVersion := NET_FW_IP_VERSION_ANY;
    FirewallObject.Protocol := Protocol;
    FirewallObject.Port := Port;
    FirewallObject.Enabled := True;
    FirewallManager := CreateOleObject('HNetCfg.FwMgr');
    FirewallProfile := FirewallManager.LocalPolicy.CurrentProfile;
    FirewallProfile.GloballyOpenPorts.Add(FirewallObject);
  except
  end;
end;    

procedure RemoveFirewallPortException(Protocol, Port: integer);
var
  FirewallManager: Variant;
  FirewallProfile: Variant;
begin
  try
    FirewallManager := CreateOleObject('HNetCfg.FwMgr');
    FirewallProfile := FirewallManager.LocalPolicy.CurrentProfile;
    FireWallProfile.GloballyOpenPorts.Remove(Port, Protocol);
  except
  end;
end;

// **************************************************************************************
// Install a service, code from:
// http://www.vincenzo.net/isxkb/index.php?title=Service
// **************************************************************************************

type
	SERVICE_STATUS = record
    	dwServiceType				: cardinal;
    	dwCurrentState				: cardinal;
    	dwControlsAccepted			: cardinal;
    	dwWin32ExitCode				: cardinal;
    	dwServiceSpecificExitCode	: cardinal;
    	dwCheckPoint				: cardinal;
    	dwWaitHint					: cardinal;
	end;
	HANDLE = cardinal;

const
	SERVICE_QUERY_CONFIG		= $1;
	SERVICE_CHANGE_CONFIG		= $2;
	SERVICE_QUERY_STATUS		= $4;
	SERVICE_START				= $10;
	SERVICE_STOP				= $20;
	SERVICE_ALL_ACCESS			= $f01ff;
	SC_MANAGER_ALL_ACCESS		= $f003f;
	SERVICE_WIN32_OWN_PROCESS	= $10;
	SERVICE_WIN32_SHARE_PROCESS	= $20;
	SERVICE_WIN32				= $30;
	SERVICE_INTERACTIVE_PROCESS = $100;
	SERVICE_BOOT_START          = $0;
	SERVICE_SYSTEM_START        = $1;
	SERVICE_AUTO_START          = $2;
	SERVICE_DEMAND_START        = $3;
	SERVICE_DISABLED            = $4;
	SERVICE_DELETE              = $10000;
	SERVICE_CONTROL_STOP		= $1;
	SERVICE_CONTROL_PAUSE		= $2;
	SERVICE_CONTROL_CONTINUE	= $3;
	SERVICE_CONTROL_INTERROGATE = $4;
	SERVICE_STOPPED				= $1;
	SERVICE_START_PENDING       = $2;
	SERVICE_STOP_PENDING        = $3;
	SERVICE_RUNNING             = $4;
	SERVICE_CONTINUE_PENDING    = $5;
	SERVICE_PAUSE_PENDING       = $6;
	SERVICE_PAUSED              = $7;

// #######################################################################################
// nt based service utilities
// #######################################################################################
function OpenSCManager(lpMachineName, lpDatabaseName: string; dwDesiredAccess :cardinal): HANDLE;
external 'OpenSCManagerW@advapi32.dll stdcall';

function OpenService(hSCManager :HANDLE;lpServiceName: string; dwDesiredAccess :cardinal): HANDLE;
external 'OpenServiceW@advapi32.dll stdcall';

function CloseServiceHandle(hSCObject :HANDLE): boolean;
external 'CloseServiceHandle@advapi32.dll stdcall';

function CreateService(hSCManager :HANDLE;lpServiceName, lpDisplayName: string;dwDesiredAccess,dwServiceType,dwStartType,dwErrorControl: cardinal;lpBinaryPathName,lpLoadOrderGroup: String; lpdwTagId : cardinal;lpDependencies,lpServiceStartName,lpPassword :string): cardinal;
external 'CreateServiceW@advapi32.dll stdcall';

function DeleteService(hService :HANDLE): boolean;
external 'DeleteService@advapi32.dll stdcall';

function StartNTService(hService :HANDLE;dwNumServiceArgs : cardinal;lpServiceArgVectors : cardinal) : boolean;
external 'StartServiceW@advapi32.dll stdcall';

function ControlService(hService :HANDLE; dwControl :cardinal;var ServiceStatus :SERVICE_STATUS) : boolean;
external 'ControlService@advapi32.dll stdcall';

function QueryServiceStatus(hService :HANDLE;var ServiceStatus :SERVICE_STATUS) : boolean;
external 'QueryServiceStatus@advapi32.dll stdcall';

function QueryServiceStatusEx(hService :HANDLE;ServiceStatus :SERVICE_STATUS) : boolean;
external 'QueryServiceStatus@advapi32.dll stdcall';

function OpenServiceManager() : HANDLE;
begin
	if UsingWinNT() = true then begin
		Result := OpenSCManager('','ServicesActive',SC_MANAGER_ALL_ACCESS);
		if Result = 0 then
			MsgBox('the servicemanager is not available', mbError, MB_OK)
	end
	else begin
			MsgBox('only nt based systems support services', mbError, MB_OK)
			Result := 0;
	end
end;

function IsServiceInstalled(ServiceName: string) : boolean;
var
	hSCM	: HANDLE;
	hService: HANDLE;
begin
	hSCM := OpenServiceManager();
	Result := false;
	if hSCM <> 0 then begin
		hService := OpenService(hSCM,ServiceName,SERVICE_QUERY_CONFIG);
        if hService <> 0 then begin
            Result := true;
            CloseServiceHandle(hService)
		end;
        CloseServiceHandle(hSCM)
	end
end;

function InstallService(FileName, ServiceName, DisplayName, Description : string;ServiceType,StartType :cardinal) : boolean;
var
	hSCM	: HANDLE;
	hService: HANDLE;
begin
	hSCM := OpenServiceManager();
	Result := false;
	if hSCM <> 0 then begin
		hService := CreateService(hSCM,ServiceName,DisplayName,SERVICE_ALL_ACCESS,ServiceType,StartType,0,FileName,'',0,'','','');
		if hService <> 0 then begin
			Result := true;
			// Win2K & WinXP supports aditional description text for services
			if Description<> '' then
				RegWriteStringValue(HKLM,'System\CurrentControlSet\Services' + ServiceName,'Description',Description);
			CloseServiceHandle(hService)
		end;
        CloseServiceHandle(hSCM)
	end
end;

function RemoveService(ServiceName: string) : boolean;
var
	hSCM	: HANDLE;
	hService: HANDLE;
begin
	hSCM := OpenServiceManager();
	Result := false;
	if hSCM <> 0 then begin
		hService := OpenService(hSCM,ServiceName,SERVICE_DELETE);
        if hService <> 0 then begin
            Result := DeleteService(hService);
            CloseServiceHandle(hService)
		end;
        CloseServiceHandle(hSCM)
	end
end;

function StartService(ServiceName: string) : boolean;
var
	hSCM	: HANDLE;
	hService: HANDLE;
begin
	hSCM := OpenServiceManager();
	Result := false;
	if hSCM <> 0 then begin
		hService := OpenService(hSCM,ServiceName,SERVICE_START);
        if hService <> 0 then begin
        	Result := StartNTService(hService,0,0);
            CloseServiceHandle(hService)
		end;
        CloseServiceHandle(hSCM)
	end;
end;

function StopService(ServiceName: string) : boolean;
var
	hSCM	: HANDLE;
	hService: HANDLE;
	Status	: SERVICE_STATUS;
begin
	hSCM := OpenServiceManager();
	Result := false;
	if hSCM <> 0 then begin
		hService := OpenService(hSCM,ServiceName,SERVICE_STOP);
        if hService <> 0 then begin
        	Result := ControlService(hService,SERVICE_CONTROL_STOP,Status);
            CloseServiceHandle(hService)
		end;
        CloseServiceHandle(hSCM)
	end;
end;

function IsServiceRunning(ServiceName: string) : boolean;
var
	hSCM	: HANDLE;
	hService: HANDLE;
	Status	: SERVICE_STATUS;
begin
	hSCM := OpenServiceManager();
	Result := false;
	if hSCM <> 0 then begin
		hService := OpenService(hSCM,ServiceName,SERVICE_QUERY_STATUS);
    	if hService <> 0 then begin
			if QueryServiceStatus(hService,Status) then begin
				Result :=(Status.dwCurrentState = SERVICE_RUNNING)
        	end;
            CloseServiceHandle(hService)
		    end;
        CloseServiceHandle(hSCM)
	end
end;

// #######################################################################################
// create an entry in the services file
// #######################################################################################
function SetupService(service, port, comment: string) : boolean;
var
	filename	: string;
	s			: string;
	lines		: TArrayOfString;
	n			: longint;
	i			: longint;
	errcode		: integer;
	servnamlen	: integer;
	error		: boolean;
begin
	if UsingWinNT() = true then
		filename := ExpandConstant('{sys}\drivers\etc\services')
	else
		filename := ExpandConstant('{win}\services');

	if LoadStringsFromFile(filename,lines) = true then begin
		Result		:= true;
		n			:= GetArrayLength(lines) - 1;
		servnamlen	:= Length(service);
		error		:= false;

		for i:=0 to n do begin
			if Copy(lines[i],1,1) <> '#' then begin
				s := Copy(lines[i],1,servnamlen);
				if CompareText(s,service) = 0 then
					exit; // found service-entry

				if Pos(port,lines[i]) > 0 then begin
					error := true;
					lines[i] := '#' + lines[i] + '   # disabled because collision with  ' + service + ' service';
				end;
			end
			else if CompareText(Copy(lines[i],2,servnamlen),service) = 0 then begin
				// service-entry was disabled
				Delete(lines[i],1,1);
				Result := SaveStringsToFile(filename,lines,false);
				exit;
			end;
		end;

		if error = true then begin
			// save disabled entries
			if SaveStringsToFile(filename,lines,false) = false then begin
				Result := false;
				exit;
			end;
		end;

		// create new service entry
		s := service + '       ' + port + '           # ' + comment + #13#10;
		if SaveStringToFile(filename,s,true) = false then begin
			Result := false;
			exit;
		end;

		if error = true then begin
			MsgBox('the ' + service + ' port was already used. The old service is disabled now. You should check the services file manually now.',mbInformation,MB_OK);
		end;
	end
	else
		Result := false;
end;


// **************************************************************************************
// Add firewall rules and install the service
// **************************************************************************************

procedure TaskKill(fileName: String);
var
	errorCode: Integer;
begin
	ShellExec('', 'taskkill', '/im ' + fileName + ' /f /t', '', SW_HIDE, ewWaitUntilTerminated, errorCode);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
	var ErrorCode : Integer;
begin
	// Remove service
	if IsServiceInstalled(OPEN_VIEWTOP_SERVICE_NAME) then begin
		StopService(OPEN_VIEWTOP_SERVICE_NAME);
		TaskKill(OPEN_VIEWTOP_SERVER_EXE);
		if not RemoveService(OPEN_VIEWTOP_SERVICE_NAME) then begin
			MsgBox('Error removing the Open Viewtop service.', mbError, MB_OK);
		end;
	end;
	TaskKill(OPEN_VIEWTOP_SERVER_EXE);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
	if CurStep = ssPostInstall then begin
		SetFirewallException('Open Viewtop', ExpandConstant('{app}') + '\' + OPEN_VIEWTOP_SERVER_EXE);

		// Install service
		if not InstallService(ExpandConstant('{app}')+'\' + OPEN_VIEWTOP_SERVER_EXE + ' -service',
								OPEN_VIEWTOP_SERVICE_NAME,
								'Open Viewtop Service',
								'Remote desktop viewer',
								SERVICE_WIN32_OWN_PROCESS,
								SERVICE_AUTO_START) then begin
				MsgBox('Error installing the Open Viewtop service.', mbError, MB_OK);
		end;
		if not StartService(OPEN_VIEWTOP_SERVICE_NAME) then begin
			MsgBox('Error starting the Open Viewtop service.', mbError, MB_OK);
		end;
	end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin  
	if CurUninstallStep = usUninstall then begin
		RemoveFirewallException(ExpandConstant('{app}')+ '\' + OPEN_VIEWTOP_SERVER_EXE);
		StopService(OPEN_VIEWTOP_SERVICE_NAME);
		TaskKill(OPEN_VIEWTOP_SERVER_EXE);
		if not RemoveService(OPEN_VIEWTOP_SERVICE_NAME) then begin
			MsgBox('Error removing the Open Viewtop service.', mbError, MB_OK);
		end;
	end;
end;
