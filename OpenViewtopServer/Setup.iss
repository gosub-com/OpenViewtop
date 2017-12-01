[Setup]
AppName=Open Viewtop
AppVersion=0.0.16
OutputDir=.
OutputBaseFilename=SetupOpenViewtop-0.0.16
UsePreviousAppDir=false
UsePreviousGroup=false
DefaultDirName={pf64}\Gosub\Open Viewtop
DefaultGroupName=Open Viewtop
AppPublisher=Gosub Software
UninstallDisplayName=Open Viewtop
UninstallDisplayIcon={app}\OpenViewtopServer.exe
LicenseFile=License.txt

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

// **************************************************************************
// The following code is for adding rules to the firewall
// http://www.vincenzo.net/isxkb/index.php?title=Adding_a_rule_to_the_Windows_firewall
// **************************************************************************

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

// **************************************************************************
// Add firewall rules
// **************************************************************************

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then begin
    SetFirewallException('Open Viewtop', ExpandConstant('{app}') + '\' + OPEN_VIEWTOP_SERVER_EXE);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin  
  if CurUninstallStep = usPostUninstall then begin
     RemoveFirewallException(ExpandConstant('{app}')+ '\' + OPEN_VIEWTOP_SERVER_EXE);
  end;
end;
