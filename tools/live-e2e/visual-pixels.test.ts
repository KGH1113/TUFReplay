import { beforeAll, expect, test } from "bun:test";
import { readFile, writeFile, readdir } from "node:fs/promises";
import { resolve } from "node:path";
import { fixtureOutput, visualRoot } from "./visual-fixtures";

const player = resolve(visualRoot,"../adofai-web-editor");
const { createCanvas, Image, Path2D, GlobalFonts } = await import(resolve(player,"node_modules/@napi-rs/canvas/index.js"));
const { default: sharp } = await import(resolve(player,"node_modules/sharp/dist/index.mjs"));
const { ReplayVisualRenderer } = await import(resolve(player,"src/rendering/replay/replay-visual.renderer.ts"));
const { UnitySpritePainter } = await import(resolve(player,"src/rendering/replay/unity-sprite.painter.ts"));
const { compileReplayVisual, ReplayPresentationEvaluator, buildTufReplayPlaybackTimeline, buildTufReplayKeyViewerTimeline } = await import(resolve(player,"src/replay/index.ts"));
const { parseTufReplayReplay } = await import(resolve(player,"src/replay/tuf-replay.parser.ts"));
const { compileProject } = await import(resolve(player,"tests/helpers.ts"));
const { parseReplayVisualBundle } = await import(resolve(player,"src/replay/replay-visual-bundle.parser.ts"));
const fontFamilies: string[] = [];
beforeAll(()=>{
  Object.assign(globalThis,{ Image, Path2D, document:{createElement:(name:string)=>{if(name!=="canvas")throw new Error(name);return createCanvas(1,1);},fonts:{add:()=>{},delete:()=>{}}},
    FontFace:class {
      constructor(public family:string,private source:string){}
      async load(){const url=/url\("(.*)"\)/.exec(this.source)![1]!;const data=Buffer.from(url.slice(url.indexOf(",")+1),"base64");
        if(!GlobalFonts.register(data,this.family))throw new Error(`Cannot decode font ${this.family}`);fontFamilies.push(this.family);return this;}
    }});
});
const sourceImage=async(name:string)=>{const image=new Image();image.src=`data:image/png;base64,${(await readFile(resolve(visualRoot,`TUFReplay/Visual/Assets/Jipper/${name}.png.base64`),"utf8")).trim()}`;await image.decode();return image;};

test("source PNG pixel oracle: native size, adjusted 9-slice corners, tint alpha and tiled ghost pattern",async()=>{
  const painter=new UnitySpritePainter();
  for(const name of ["KeyBackground","KeyOutline","GhostRain","ProgressBackground"]){
    const image=await sourceImage(name), w=image.naturalWidth,h=image.naturalHeight;
    const actual=createCanvas(w,h), expected=createCanvas(w,h);
    expected.getContext("2d").drawImage(image,0,0);
    // Source white sprites tinted white must retain every pixel, including transparent rounded corners.
    painter.draw(actual.getContext("2d"),image,"#ffffff",0,0,w,h,1,name==="GhostRain",name==="GhostRain"?2:name==="ProgressBackground"?10:11);
    expect(Buffer.from(actual.getContext("2d").getImageData(0,0,w,h).data).equals(Buffer.from(expected.getContext("2d").getImageData(0,0,w,h).data))).toBe(true);
  }
  const image=await sourceImage("KeyBackground");
  const actual=createCanvas(150,50),expected=createCanvas(11,11);
  painter.draw(actual.getContext("2d"),image,"#ff000080",0,0,150,50);
  expected.getContext("2d").drawImage(image,0,0,11,11,0,0,11,11);
  const corner=actual.getContext("2d").getImageData(0,0,11,11).data, oracle=expected.getContext("2d").getImageData(0,0,11,11).data;
  for(let i=0;i<corner.length;i+=4){expect(corner[i+1]).toBe(0);expect(Math.abs(corner[i+3]!-Math.round(oracle[i+3]! *128/255))).toBeLessThanOrEqual(1);}
  const ghost=await sourceImage("GhostRain"),tile=createCanvas(160,180);
  painter.draw(tile.getContext("2d"),ghost,"#ffffff",0,0,160,180,1,true,2);
  const period=ghost.naturalWidth-4;
  if(period+10<158) expect(Buffer.from(tile.getContext("2d").getImageData(5,30,5,5).data)).toEqual(Buffer.from(tile.getContext("2d").getImageData(5+period,30,5,5).data));
});

test("all real importer bundles decode and paint through the replay compiler and renderer",async()=>{
  const files=(await readdir(fixtureOutput)).filter(name=>name.endsWith(".json") && !name.endsWith(".api.json"));
  expect(files.length).toBe(6);
  const replay=parseTufReplayReplay({metadata:JSON.stringify({gameplayStartSongPosition:0,effectivePitch:1,difficulty:"Strict",wonTimeUs:1000000,inputNativePlatform:"macos",inputKeySpace:"os-native-key-code",terminalTimeUs:2000000}),inputs:"100000,0,1,0,0\n100000,11,1,0,0\n100000,56,1,0,0\n600000,0,0,0,0\n600000,11,0,0,0\n600000,56,0,0,0\n",hits:"0,0,0,0,0,0,0,0,0,0,0,3,100000\n"});
  const timeline=buildTufReplayPlaybackTimeline(compileProject({angleData:[0,90,180],settings:{bpm:120}}),replay);
  for(const name of files){
    // Server roundtrip output is mandatory in this integration suite.
    const raw=JSON.parse(await readFile(resolve(fixtureOutput,name.replace(".json",".api.json")),"utf8"));
    const original=JSON.parse(await readFile(resolve(fixtureOutput,name),"utf8"));
    expect(raw).toEqual(original);
    const bundle=parseReplayVisualBundle(raw), visual=compileReplayVisual(bundle);
    if(bundle.source==="jipper-resourcepack" && bundle.kind==="keyviewer") {
      expect(visual.keySlots).toHaveLength(20);
      expect(visual.keySlots[0]).toMatchObject({keyName:"Tab",width:50,height:50});
      expect(visual.keySlots[0]!.y).toBeCloseTo(355.7639);
      expect(visual.keySlots[0]!.style.fill).toBe("rgba(255, 0, 0, 0.1960784)");
      expect(visual.keySlots.some((slot:any)=>slot.label==="?")).toBe(false);
      expect(visual.keySlots.find((slot:any)=>slot.id==="jipper-key-12")?.x).toBe(0);
      expect(visual.nodes.find((node:any)=>node.id==="jipper-total")?.sourceKeySprites).toBeDefined();
    }
    if(bundle.source==="impl-resourcepack") {
      const fallback=bundle.assets.find((a:any)=>a.path==="assets/builtin/adofai-cjk.woff");
      expect(fallback?.media_type).toBe("font/woff");
      expect(Buffer.from(fallback!.data_base64,"base64")).toEqual(await readFile(resolve(visualRoot,"TUFReplay/Visual/Assets/Fonts/adofai-cjk.woff")));
      expect(bundle.assets.some((a:any)=>a.path.startsWith("assets/uploaded/"))).toBe(false);
    }
    expect(visual.assetPaths.length).toBeGreaterThan(0);
    for(const path of visual.assetPaths)expect(bundle.assets.some((a:any)=>a.path===path)).toBe(true);
    const presentation={keyviewer:bundle.kind==="keyviewer"?visual:null,overlay:bundle.kind==="overlay"?visual:null};
    const renderer=new ReplayVisualRenderer();
    const sources=new Map<string,string>();
    for(const a of bundle.assets){
      // Skia's test Image lacks AVIF decoding; libvips supplies that browser codec.
      // The compiler, persisted bundle, and public response still use original AVIF bytes.
      sources.set(a.path,a.media_type==="image/avif" ? `data:image/png;base64,${(await sharp(Buffer.from(a.data_base64,"base64")).png().toBuffer()).toString("base64")}` : `data:${a.media_type};base64,${a.data_base64}`);
    }
    renderer.setAssetResolver((path:string)=>sources.get(path)??null);
    await renderer.loadAssets(presentation);
    const evaluator=new ReplayPresentationEvaluator();evaluator.init({presentation,keyTimeline:buildTufReplayKeyViewerTimeline(replay),timeline,durationUs:2000000,songTitle:"한글 中文 日本語",baseBpm:120});
    const gpu={autoClear:true,clearDepth:()=>{},render:()=>{}};
    const snapshots:Buffer[]=[];
    for(const time of [0,300000,700000]){
      const frame=evaluator.evaluateAt(time,true);renderer.render(gpu,frame);
      if(bundle.source==="jipper-resourcepack" && bundle.kind==="keyviewer") {
        const bound=frame.keyviewer.slots.find((slot:any)=>slot.id==="jipper-key-12");
        expect(bound?.pressed).toBe(time===300000);
        expect(bound?.count).toBe(time===0?0:1);
        const shift=frame.keyviewer.slots.find((slot:any)=>slot.id==="jipper-key-13");
        expect(shift?.keyName).toBe("LShift");
        expect(shift?.label).toBe("LShift");
        expect(shift?.pressed).toBe(time===300000);
        expect(shift?.count).toBe(time===0?0:1);
      }
      const canvas=renderer.canvas, pixels=canvas.getContext("2d").getImageData(0,0,1920,1080).data;
      let ink=0;for(let i=3;i<pixels.length;i+=4)if(pixels[i])ink++;
      expect(ink).toBeGreaterThan(100);
      const png=canvas.toBuffer("image/png");snapshots.push(png);await writeFile(resolve(fixtureOutput,`${name}-${time}.png`),png);
      if(name.startsWith("dmnote")||name.startsWith("impl-dmnote")) {
        const sprite=frame.keyviewer.nodes.find((n:any)=>n.id==="mascot");expect(sprite?.visible).toBe(true);
        if(time===300000)expect(sprite.rotation).toBeCloseTo(15);
      }
    }
    if(snapshots[0]!.equals(snapshots[1]!)) throw new Error(`No visual state change: ${name}; ${JSON.stringify(evaluator.evaluateAt(300000).keyviewer?.nodes.map((n:any)=>({id:n.id,x:n.x,y:n.y,width:n.width,height:n.height,rotation:n.rotation,opacity:n.style.opacity,image:n.imagePath})))}`);
    if(bundle.source==="jipper-keyviewer"||bundle.source==="impl-resourcepack"){
      const families=[...visual.nodes,...visual.keySlots].map((n:any)=>n.style.fontFamily);
      expect(families.some((name:string)=>name.includes(","))).toBe(true);
    }
    renderer.dispose();
  }
  expect(fontFamilies.some(f=>f.includes("LeeSeoyun"))).toBe(true);
  expect(fontFamilies.some(f=>f.includes("DungGeunMo"))).toBe(true);
  expect(fontFamilies.some(f=>f.includes("adofai_cjk"))).toBe(true);
},120000);
