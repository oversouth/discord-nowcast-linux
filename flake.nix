{
  description = "discord-nowcast: track from any Linux app or Yandex Music account in Discord status";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
    flake-utils.url = "github:numtide/flake-utils";
  };

  outputs = { self, nixpkgs, flake-utils }:
    flake-utils.lib.eachDefaultSystem (system:
      let
        pkgs = import nixpkgs { inherit system; };
        discord-nowcast = pkgs.callPackage ./package.nix { };
      in
      {
        packages = {
          default = discord-nowcast;
          discord-nowcast = discord-nowcast;
        };

        apps = {
          default = flake-utils.lib.mkApp {
            drv = discord-nowcast;
          };
        };

        devShells.default = pkgs.mkShell {
          packages = [
            pkgs.python3
            pkgs.python3Packages.setuptools
          ];
        };
      }
    );
}
