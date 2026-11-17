// Run on macOS. Produces explicitly ad-hoc-signed development DMG and ZIP files.
const {build,Platform,Arch}=require('electron-builder');
const pkg=require('../package.json');
const arch=process.argv[2]||'arm64';
if(process.platform!=='darwin'){console.error('macOS packaging requires a Mac or GitHub macOS runner.');process.exit(1);}
if(!['arm64','x64'].includes(arch)){console.error('Architecture must be arm64 or x64.');process.exit(1);}
build({targets:Platform.MAC.createTarget(['dmg','zip'],Arch[arch]),publish:'never',config:{...pkg.build,directories:{output:'release/macos'},mac:{...pkg.build.mac,identity:'-',artifactName:'NetPeep-${version}-macOS-${arch}.${ext}'}}}).catch(error=>{console.error(error);process.exitCode=1;});
