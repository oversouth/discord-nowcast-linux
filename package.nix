{ lib, python3Packages }:

python3Packages.buildPythonApplication rec {
  pname = "discord-nowcast";
  version = "2.0.0";

  src = lib.cleanSource ./.;

  pyproject = true;

  build-system = [
    python3Packages.setuptools
  ];

  meta = with lib; {
    description = "Show playing track from Linux MPRIS and Yandex Music account in Discord status";
    homepage = "https://github.com/oversouth/discord-nowcast-linux";
    license = licenses.mit;
    platforms = platforms.linux;
    mainProgram = "discord-nowcast";
  };
}
