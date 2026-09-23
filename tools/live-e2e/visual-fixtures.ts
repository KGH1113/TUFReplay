import { mkdir, mkdtemp, readFile, writeFile, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { resolve } from "node:path";

export const visualRoot = resolve(import.meta.dir, "../..");
export const fixtureOutput = resolve(visualRoot, "build/visual-fixtures");
export async function importVisual(input: unknown): Promise<any> {
  const child = Bun.spawn(["rtk", "proxy", "./scripts/run.sh", "visual-import"], { cwd: visualRoot, stdin: "pipe", stdout: "pipe", stderr: "pipe" });
  child.stdin.write(JSON.stringify(input)); child.stdin.end();
  const [out, err, status] = await Promise.all([new Response(child.stdout).text(), new Response(child.stderr).text(), child.exited]);
  if (status) throw new Error(`Importer failed: ${err}`);
  const result = JSON.parse(out);
  if (result.error) throw new Error(JSON.stringify(result.error));
  return result;
}

/** Real upstream bytes; generated installations never touch the user's installed mods. */
export async function generateVisualFixtures(): Promise<string[]> {
  const temp = await mkdtemp(resolve(tmpdir(), "tuf-visual-fixtures-"));
  const originalJkv = process.env["JKV_SOURCE_ROOT"] ?? "/private/tmp/tuf-jipper-keyviewer/JipperKeyViewer";
  const cjk = await readFile(resolve(originalJkv, "assets/cjkFonts-regular-normalized.otf"));
  const maple = await readFile(resolve(visualRoot, "TUFReplay.Unity/Assets/Fonts/MAPLESTORY_OTF_BOLD.OTF"));
  const png = (await readFile(resolve(visualRoot, "TUFReplay/Visual/Assets/Jipper/KeyBackground.png.base64"), "utf8")).trim();
  const pose = (await readFile(resolve(visualRoot, "TUFReplay/Visual/Assets/Jipper/GhostRain.png.base64"), "utf8")).trim();
  const { createCanvas } = await import(resolve(visualRoot,"../adofai-web-editor/node_modules/@napi-rs/canvas/index.js"));
  const iconCanvas=createCanvas(16,16),paint=iconCanvas.getContext("2d");
  paint.fillStyle="#ff00aa";paint.fillRect(0,0,16,16);
  const iconPng=iconCanvas.toBuffer("image/png"),header=Buffer.alloc(22);
  header.writeUInt16LE(1,2);header.writeUInt16LE(1,4);header[6]=16;header[7]=16;header.writeUInt16LE(1,10);header.writeUInt16LE(32,12);header.writeUInt32LE(iconPng.length,14);header.writeUInt32LE(22,18);
  const ico=Buffer.concat([header,iconPng]).toString("base64"),avif=(await iconCanvas.encode("avif")).toString("base64");
  const files: string[] = [];
  try {
    await mkdir(fixtureOutput, { recursive: true });
    const save = async (name: string, input: unknown) => {
      const bundle = await importVisual(input);
      const path = resolve(fixtureOutput, `${name}.json`);
      await writeFile(path, JSON.stringify(bundle)); files.push(path);
    };
    for (const [source,id] of [["jipper-resourcepack","JipperResourcePack"],["jipper-keyviewer","JipperKeyViewer"],["impl-resourcepack","ImplResourcePack"]]) {
      const installation=resolve(temp,source!);
      await mkdir(installation,{recursive:true});
      await writeFile(resolve(installation,"Info.json"),JSON.stringify({Id:id,Version:source==="jipper-keyviewer"?"1.7.2":"1.5.2.0"}));
      if(source==="jipper-resourcepack") {
        const settings=JSON.parse(await readFile(resolve(visualRoot,"tools/live-e2e/fixtures/jipper-resourcepack-settings.json"),"utf8"));
        Object.assign(settings.Feature,{Status:{Setting:{ShowProgressBar:true}},BPM:{Setting:{}},Combo:{Setting:{}},Judgement:{Setting:{}}});
        await writeFile(resolve(installation,"Settings.json"), JSON.stringify(settings));
        await save(`${source}-keyviewer`,{source,kind:"keyviewer",installation});
        await save(`${source}-overlay`,{source,kind:"overlay",installation});
      } else if(source==="jipper-keyviewer") {
        await mkdir(resolve(installation,"config/profiles"),{recursive:true});await mkdir(resolve(installation,"assets"));
        await writeFile(resolve(installation,"assets/MAPLESTORY_OTF_BOLD.OTF"),maple);
        await writeFile(resolve(installation,"assets/cjkFonts-regular-normalized.otf"),cjk);
        await writeFile(resolve(installation,"config/settings.json"),JSON.stringify({Version:6,CurrentProfile:"Portable"}));
        await writeFile(resolve(installation,"config/profiles/Portable.json"),JSON.stringify({FontName:"MapleStory",KeyViewerStyle:"Custom",Size:1,UseRain:true,UseGhostRain:true,CustomNodes:[{KeyBind:65,CustomText:"한글 中文 日本語",X:100,Y:200,Width:160,Height:60},{KeyBind:66,X:300,Y:200,Width:60,Height:60,ImagePath:"https://fixture.invalid/custom.png"}]}));
        await save(source,{source,kind:"keyviewer",installation,assets:[{reference:"https://fixture.invalid/custom.png",data_base64:png}]});
      } else {
        const input={source,kind:"overlay",installation};
        const inspection=await importVisual({...input,inspect:true});
        if(inspection.missing_assets.length!==0)throw new Error("Default ImplResourcePack must not require any attachments");
        await save(source,input);
      }
    }
    for(const source of ["dmnote","impl-dmnote"]) {
      const preset={version:"2.0.2",selectedKeyType:"main",keys:{main:["A","B","C","D","E"]},keyPositions:{main:["Pretendard Variable","SUIT","IsYun","RoundedFixedsys","Pretendard"].map((fontFamily,i)=>({id:`key-${i}`,dx:i*90,dy:300,width:80,height:60,fontFamily,label:"한글",noteEffectEnabled:true}))},spritePositions:{main:[{id:"mascot",dx:80,dy:100,width:80,height:80,baseImage:"https://fixture.invalid/base.png",referenceNaturalSize:{source:"https://fixture.invalid/base.png",width:100,height:100},transitionMs:100,transitionEasing:"ease-out",poses:[{poseId:"pressed",triggers:["key-0"],transform:{x:30,y:-20,scale:1.2,rotation:15},imageOverride:"blob:pose",imageOverrideMetrics:{source:"blob:pose",width:100,height:100}}]}]},customFonts:[{enabled:true,type:"web",name:"PortableCustom",cssContent:"@font-face{font-family:PortableCustom;src:url(https://fixture.invalid/font.otf)}"}],customCSS:{content:'.key {background-image:url(https://fixture.invalid/background.png)}'},sounds:["excluded.wav"],embeddedLocalSounds:[{dataBase64:"invalid"}]};
      Object.assign(preset.keyPositions.main[0]!,{inactiveImage:"https://fixture.invalid/key.ico"});
      Object.assign(preset.keyPositions.main[1]!,{inactiveImage:"https://fixture.invalid/key.avif"});
      Object.assign(preset,{statPositions:{main:[{id:"custom-font",statType:"kps",fontFamily:"PortableCustom",dx:0,dy:450,width:200,height:60}]}});
      await save(source,{source,kind:"keyviewer",presetJson:JSON.stringify(preset),assets:[{reference:"https://fixture.invalid/base.png",data_base64:png},{reference:"blob:pose",data_base64:pose},{reference:"https://fixture.invalid/background.png",data_base64:png},{reference:"https://fixture.invalid/font.otf",data_base64:maple.toString("base64")},{reference:"https://fixture.invalid/key.ico",data_base64:ico},{reference:"https://fixture.invalid/key.avif",data_base64:avif}]});
    }
    return files;
  } finally { await rm(temp,{recursive:true,force:true}); }
}
if(import.meta.main) console.log(JSON.stringify(await generateVisualFixtures()));
