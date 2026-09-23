import { mkdir, readdir } from "node:fs/promises";
import { join } from "node:path";
import { Database } from "bun:sqlite";
const game = "/Users/kgh/Library/Application Support/Steam/steamapps/common/A Dance of Fire and Ice";
const db = new Database(join(game,"tufreplay-backup/TUFReplay/Data/tufreplay.sqlite"),{readonly:true});
const run = db.query("select id,result,x_accuracy,judgment_difficulty,judgment_too_early from runs where id=?").get("f689436ba19b4f4090dc704b8b785de2") as {id:string;result:string;x_accuracy:number;judgment_difficulty:number;judgment_too_early:number};
db.close();
if (run?.result!=="cleared" || run.judgment_too_early!==3) throw new Error("Requested clear does not match");
const source = "/private/tmp/tuf-visuals-20260916/adofai-web-editor/tests/fixtures/webgpu/real-asgore";
const out = "/Users/kgh/dev/src/adofai-web-editor/public/test-creplay/asgore";
await mkdir(out,{recursive:true});
for(const file of ["run-inputs.csv","run-hits.csv","Asgore.adofai"]) await Bun.write(join(out,file),Bun.file(join(source,file)));
const metadata=await Bun.file(join(source,"run-metadata.json")).json();
metadata.difficulty=run.judgment_difficulty;metadata.xAccuracy=run.x_accuracy;
await Bun.write(join(out,"run-metadata.json"),JSON.stringify(metadata));
const base="http://127.0.0.1:5151";
const manifest=await fetch(`${base}/api/v1/replays/1ee0248d-1173-4e02-8ad0-b20b8d0a6ec1?format=3`).then(r=>r.json());
for(const kind of ["keyviewer","overlay"]){const descriptor=manifest.visuals[kind];const response=await fetch(base+descriptor.url);if(!response.ok)throw new Error("Visual download failed");await Bun.write(join(out,`${kind}.json`),await response.arrayBuffer());}
await Bun.write(join(out,"visuals.json"),JSON.stringify(manifest.visuals));
let count=0;
async function copyAssets(from:string, relative="") {
  for(const entry of await readdir(from,{withFileTypes:true})){
    if(entry.isSymbolicLink())continue;
    const name=join(relative,entry.name);
    if(entry.isDirectory())await copyAssets(join(from,entry.name),name);
    else if(/\.(ogg|wav|mp3|png|jpg|jpeg|webp|gif)$/i.test(entry.name)) {await mkdir(join(out,"assets",relative),{recursive:true});await Bun.write(join(out,"assets",name),Bun.file(join(from,entry.name)));count++;}
  }
}
await copyAssets(join(game,"Mods/TUFHelperLite/Downloads/tuf-6299"));
console.log({run:run.id,tooEarly:run.judgment_too_early,out,assets:count});
