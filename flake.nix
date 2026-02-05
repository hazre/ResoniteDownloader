{
  inputs = {
    flake-parts.url = "github:hercules-ci/flake-parts";
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
  };

  outputs = inputs @ {flake-parts, ...}: let
    csproj = builtins.readFile ./ResoniteDownloader/ResoniteDownloader.csproj;

    versionMatch = builtins.match ".*<Version>(.*?)</Version>.*" csproj;
    version = builtins.elemAt versionMatch 0;
    
    descriptionMatch = builtins.match ".*<Description>(.*?)</Description>.*" csproj;
    description = builtins.elemAt descriptionMatch 0;
  in
    flake-parts.lib.mkFlake {inherit inputs;} {
      imports = [];
      systems = ["x86_64-linux" "aarch64-linux" "aarch64-darwin" "x86_64-darwin"];
      perSystem = {
        config,
        self',
        inputs',
        pkgs,
        system,
        ...
      }: let

        dotnetVersion = pkgs.dotnetCorePackages.sdk_10_0;
        homepage = "https://github.com/hazre/ResoniteDownloader";
        releaseUrl = "${homepage}/releases/tag/v${version}";

        resonitedownloader = pkgs.buildDotnetModule {
          pname = "ResoniteDownloader";
          inherit version;

          src = ./.;

          projectFile = "ResoniteDownloader/ResoniteDownloader.csproj";
          nugetDeps = ./deps.json;

          dotnet-sdk = dotnetVersion;
          dotnet-runtime = pkgs.dotnetCorePackages.runtime_10_0;

          meta = {
            inherit description;
            inherit homepage;
            downloadPage = releaseUrl;
            changelog = releaseUrl;
            license = pkgs.lib.licenses.mit;
            mainProgram = "ResoniteDownloader";
            platforms = pkgs.lib.platforms.linux;
          };
        };
      in {
        devShells.default = pkgs.mkShellNoCC {
          packages = [dotnetVersion];
        };

        packages = {
          default = resonitedownloader;

          debug = resonitedownloader.overrideAttrs (self: super: {
            dotnetBuildType = "Debug";
          });
        };

        # To update ./deps.json, run `nix run .#update-deps`
        apps.update-deps = {
          type = "app";
          program = toString (pkgs.writeShellScript "update-deps" "${resonitedownloader.passthru.fetch-deps} deps.json");
        };
      };
      flake = {
        inherit description;
      };
    };
}
