function OnAddinLoad(ribbonUI) {
  return true;
}

function GetTimerExecutablePath() {
  var href = String(window.location.href);
  var base = href.substring(0, href.lastIndexOf("/") + 1);
  var path = decodeURIComponent(base + "WpsReportTimer.exe");
  path = path.replace(/^file:\/\//, "");
  if (path.charAt(0) === "/" && path.charAt(2) === ":") {
    path = path.substring(1);
  }
  return path;
}

function OnStartTimer(control) {
  try {
    if (typeof wps === "undefined" || !wps.OAAssist || !wps.OAAssist.ShellExecute) {
      alert("当前 WPS 版本不支持调用本地计时器。请使用支持 OAAssist.ShellExecute 的桌面版 WPS。");
      return;
    }
    var executable = GetTimerExecutablePath();
    var result = wps.OAAssist.ShellExecute(executable, "");
    if (typeof result === "number" && result <= 32) {
      alert("计时器启动失败，ShellExecute 返回代码：" + result + "\n请重新安装 WPS 报告计时器加载项。");
    }
  } catch (error) {
    alert("计时器启动失败：" + (error.message || error) + "\n请重新安装加载项，或检查 WPS 的安全提示。");
  }
}
