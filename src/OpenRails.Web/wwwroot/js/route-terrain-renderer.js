let gl, program, canvasRef, storedVertices, storedIndices, storedTrackLines;
let rotation = 0, pitch = 0.72, zoom = 1, dragging = false, lastX = 0, lastY = 0;
let wheelHandler, pointerDownHandler, pointerMoveHandler, pointerUpHandler, resizeHandler;\nconst activePointers = new Map();\nlet pinchStartDistance = 0, pinchStartZoom = 1;

export function initialize(canvas) {
  canvasRef = canvas;
  resizeCanvas();
  wheelHandler = e => { e.preventDefault(); zoom = Math.max(0.35, Math.min(5, zoom * (e.deltaY < 0 ? 1.12 : 0.89))); redraw(); };
  pointerDownHandler = e => { dragging=true; lastX=e.clientX; lastY=e.clientY; canvas.setPointerCapture(e.pointerId); };
  pointerMoveHandler = e => { if(dragging){ rotation += (e.clientX-lastX)*0.008; pitch = Math.max(0.15, Math.min(1.45, pitch+(e.clientY-lastY)*0.006)); lastX=e.clientX; lastY=e.clientY; redraw(); } };
  pointerUpHandler = () => { dragging=false; };
  resizeHandler = () => redraw();
  canvas.addEventListener('wheel', wheelHandler, { passive:false });
  canvas.addEventListener('pointerdown', pointerDownHandler);
  canvas.addEventListener('pointermove', pointerMoveHandler);
  canvas.addEventListener('pointerup', pointerUpHandler);
  canvas.addEventListener('pointercancel', pointerUpHandler);
  window.addEventListener('resize', resizeHandler);
  gl = canvas.getContext("webgl2", { antialias: true, alpha: false });
  if (!gl) throw new Error("WebGL2 is not supported by this browser.");
  const vertex = compile(gl.VERTEX_SHADER, `#version 300 es
    in vec3 position;
    in vec3 color;
    out vec3 vColor;
    void main(){ gl_Position=vec4(position,1.0); vColor=color; }`);
  const fragment = compile(gl.FRAGMENT_SHADER, `#version 300 es
    precision highp float;
    in vec3 vColor;
    out vec4 outColor;
    void main(){ outColor=vec4(vColor,1.0); }`);
  program=gl.createProgram(); gl.attachShader(program,vertex); gl.attachShader(program,fragment); gl.linkProgram(program);
  if(!gl.getProgramParameter(program,gl.LINK_STATUS)) throw new Error(gl.getProgramInfoLog(program));
  gl.enable(gl.DEPTH_TEST);
}
export function cleanup() {
  if (canvasRef) {
    canvasRef.removeEventListener('wheel', wheelHandler);
    canvasRef.removeEventListener('pointerdown', pointerDownHandler);
    canvasRef.removeEventListener('pointermove', pointerMoveHandler);
    canvasRef.removeEventListener('pointerup', pointerUpHandler);
    canvasRef.removeEventListener('pointercancel', pointerUpHandler);
  }
  if (resizeHandler) window.removeEventListener('resize', resizeHandler);
  if (gl) {
    for (const texture of terrainTextures) gl.deleteTexture(texture);
    if (program) gl.deleteProgram(program);
    if (terrainProgram) gl.deleteProgram(terrainProgram);
  }
  gl = program = terrainProgram = canvasRef = null;
  terrainTextures = [];
  uploadedTerrainPixels = storedTerrainPatches = storedTerrainPixels = storedTerrainLines = storedTrackMesh = null;
  storedVertices = storedIndices = storedTrackLines = null;
  wheelHandler = pointerDownHandler = pointerMoveHandler = pointerUpHandler = resizeHandler = null;\n  activePointers.clear(); pinchStartDistance = 0;
}
function pointerDistance(a,b){const dx=a.x-b.x,dy=a.y-b.y;return Math.hypot(dx,dy);}\nfunction compile(type,source){const s=gl.createShader(type);gl.shaderSource(s,source);gl.compileShader(s);if(!gl.getShaderParameter(s,gl.COMPILE_STATUS))throw new Error(gl.getShaderInfoLog(s));return s;}

export function drawTerrain(vertices, indices, trackLines = []) {
  storedVertices=vertices; storedIndices=indices; storedTrackLines=trackLines; redraw();
}
function resizeCanvas(){ const dpr=window.devicePixelRatio||1; const w=Math.max(1,Math.floor(canvasRef.clientWidth*dpr)),h=Math.max(1,Math.floor(canvasRef.clientHeight*dpr)); if(canvasRef.width!==w||canvasRef.height!==h){canvasRef.width=w;canvasRef.height=h;}}
function redraw(){
  if(storedTerrainPatches){drawTerrainTextured(storedTerrainPatches,storedTerrainPixels,storedTerrainLines,storedTrackMesh);return;}
  resizeCanvas();
  const vertices=storedVertices,indices=storedIndices,trackLines=storedTrackLines||[];
  if(!gl || !vertices || !vertices.length || !indices || !indices.length) return;
  const count=vertices.length/3;
  let minX=Infinity,maxX=-Infinity,minZ=Infinity,maxZ=-Infinity,minY=Infinity,maxY=-Infinity;
  for(let i=0;i<count;i++){const x=vertices[i*3],y=vertices[i*3+1],z=vertices[i*3+2];minX=Math.min(minX,x);maxX=Math.max(maxX,x);minZ=Math.min(minZ,z);maxZ=Math.max(maxZ,z);minY=Math.min(minY,y);maxY=Math.max(maxY,y);}
  for(let i=0;i<trackLines.length;i+=3){minX=Math.min(minX,trackLines[i]);maxX=Math.max(maxX,trackLines[i]);minZ=Math.min(minZ,trackLines[i+2]);maxZ=Math.max(maxZ,trackLines[i+2]);minY=Math.min(minY,trackLines[i+1]);maxY=Math.max(maxY,trackLines[i+1]);}
  const centerX=(minX+maxX)/2,centerZ=(minZ+maxZ)/2;
  const rotated=(x,z)=>{const dx=x-centerX,dz=z-centerZ;return [dx*Math.cos(rotation)-dz*Math.sin(rotation),dx*Math.sin(rotation)+dz*Math.cos(rotation)];};
  let rminX=Infinity,rmaxX=-Infinity,rminZ=Infinity,rmaxZ=-Infinity;
  for(let i=0;i<count;i++){const q=rotated(vertices[i*3],vertices[i*3+2]);rminX=Math.min(rminX,q[0]);rmaxX=Math.max(rmaxX,q[0]);rminZ=Math.min(rminZ,q[1]);rmaxZ=Math.max(rmaxZ,q[1]);}
  for(let i=0;i<trackLines.length;i+=3){const q=rotated(trackLines[i],trackLines[i+2]);rminX=Math.min(rminX,q[0]);rmaxX=Math.max(rmaxX,q[0]);rminZ=Math.min(rminZ,q[1]);rmaxZ=Math.max(rmaxZ,q[1]);}
  const span=Math.max(1,rmaxX-rminX,rmaxZ-rminZ),aspect=canvasRef.width/canvasRef.height;
  const packed=new Float32Array(count*6);
  for(let i=0;i<count;i++){
    const x=vertices[i*3],y=vertices[i*3+1],z=vertices[i*3+2],q=rotated(x,z);
    let px=((q[0]-rminX)/span)*1.8-0.9, py=((q[1]-rminZ)/span)*1.8-0.9;
    px*=zoom;py*=zoom;
    if(aspect>1) py/=aspect; else px*=aspect;
    packed[i*6]=px; packed[i*6+1]=py; packed[i*6+2]=0;
    const h=maxY-minY>0?(y-minY)/(maxY-minY):0.5;
    packed[i*6+3]=0.18+0.32*h; packed[i*6+4]=0.38+0.38*h; packed[i*6+5]=0.16+0.22*h;
  }
  const vao=gl.createVertexArray();gl.bindVertexArray(vao);
  const vb=gl.createBuffer();gl.bindBuffer(gl.ARRAY_BUFFER,vb);gl.bufferData(gl.ARRAY_BUFFER,packed,gl.STATIC_DRAW);
  const p=gl.getAttribLocation(program,"position");gl.enableVertexAttribArray(p);gl.vertexAttribPointer(p,3,gl.FLOAT,false,24,0);
  const c=gl.getAttribLocation(program,"color");gl.enableVertexAttribArray(c);gl.vertexAttribPointer(c,3,gl.FLOAT,false,24,12);
  const ib=gl.createBuffer();gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER,ib);gl.bufferData(gl.ELEMENT_ARRAY_BUFFER,new Uint32Array(indices),gl.STATIC_DRAW);
  gl.viewport(0,0,canvasRef.width,canvasRef.height);gl.clearColor(0.07,0.09,0.12,1);gl.clear(gl.COLOR_BUFFER_BIT|gl.DEPTH_BUFFER_BIT);
  gl.useProgram(program);gl.drawElements(gl.TRIANGLES,indices.length,gl.UNSIGNED_INT,0);
  gl.deleteBuffer(ib);
  if(trackLines.length){
    const linePacked=new Float32Array(trackLines.length/3*6);
    for(let i=0;i<trackLines.length/3;i++){
      const x=trackLines[i*3],z=trackLines[i*3+2],q=rotated(x,z);
      let px=((q[0]-rminX)/span)*1.8-0.9,py=((q[1]-rminZ)/span)*1.8-0.9;
      px*=zoom;py*=zoom;if(aspect>1)py/=aspect;else px*=aspect;
      linePacked[i*6]=px;linePacked[i*6+1]=py;linePacked[i*6+2]=0.01;
      linePacked[i*6+3]=0.88;linePacked[i*6+4]=0.88;linePacked[i*6+5]=0.9;
    }
    const lineBuffer=gl.createBuffer();gl.bindBuffer(gl.ARRAY_BUFFER,lineBuffer);gl.bufferData(gl.ARRAY_BUFFER,linePacked,gl.STREAM_DRAW);
    gl.vertexAttribPointer(p,3,gl.FLOAT,false,24,0);gl.vertexAttribPointer(c,3,gl.FLOAT,false,24,12);
    gl.drawArrays(gl.LINES,0,trackLines.length/3);
    gl.deleteBuffer(lineBuffer);
  }
  gl.bindVertexArray(null);
  gl.deleteBuffer(vb);
  gl.deleteVertexArray(vao);
}


// Textured route path: one draw batch per original terrain patch so each patch
// keeps its source shader texture and source UV coordinates.
let terrainProgram, terrainTextures = [], uploadedTerrainPixels, storedTerrainPatches, storedTerrainPixels, storedTerrainLines, storedTrackMesh;
export function drawTerrainTextured(patches, textures, trackLines = [], trackMesh = null) {
  storedTerrainPatches=patches; storedTerrainPixels=textures; storedTerrainLines=trackLines; storedTrackMesh=trackMesh;
  if (!gl) return;
  if (!terrainProgram) {
    const vertex = compile(gl.VERTEX_SHADER, `#version 300 es
      in vec3 position; in vec2 uv; out vec2 vUv;
      void main(){ gl_Position=vec4(position,1.0); vUv=uv; }`);
    const fragment = compile(gl.FRAGMENT_SHADER, `#version 300 es
      precision highp float; in vec2 vUv; uniform sampler2D terrainTexture;
      uniform bool hasTexture; out vec4 outColor;
      void main(){ outColor=hasTexture ? texture(terrainTexture,vUv) : vec4(0.22,0.42,0.20,1.0); }`);
    terrainProgram=gl.createProgram();
    gl.attachShader(terrainProgram,vertex); gl.attachShader(terrainProgram,fragment); gl.linkProgram(terrainProgram);
    if(!gl.getProgramParameter(terrainProgram,gl.LINK_STATUS)) throw new Error(gl.getProgramInfoLog(terrainProgram));
  }
  if (uploadedTerrainPixels !== textures) {
    for (const oldTexture of terrainTextures) gl.deleteTexture(oldTexture);
    terrainTextures = textures.map(source => {
      const texture=gl.createTexture(); gl.bindTexture(gl.TEXTURE_2D,texture);
      gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL,true);
      gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA,source.width,source.height,0,gl.RGBA,gl.UNSIGNED_BYTE,new Uint8Array(source.pixels));
      gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MIN_FILTER,gl.LINEAR);
      gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MAG_FILTER,gl.LINEAR);
      gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_S,gl.REPEAT);
      gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_T,gl.REPEAT);
      return texture;
    });
    uploadedTerrainPixels = textures;
  }
  const all = patches;
  if(!all.length) return;
  let minX=Infinity,maxX=-Infinity,minZ=Infinity,maxZ=-Infinity,minY=Infinity,maxY=-Infinity;
  for(const patch of all) for(let i=0;i<patch.vertices.length;i+=5){
    const x=patch.vertices[i],y=patch.vertices[i+1],z=patch.vertices[i+2];
    minX=Math.min(minX,x);maxX=Math.max(maxX,x);minY=Math.min(minY,y);maxY=Math.max(maxY,y);minZ=Math.min(minZ,z);maxZ=Math.max(maxZ,z);
  }
  const centerX=(minX+maxX)/2,centerY=(minY+maxY)/2,centerZ=(minZ+maxZ)/2;
  const span=Math.max(1,maxX-minX,maxZ-minZ),aspect=canvasRef.width/canvasRef.height;
  const cos=Math.cos(rotation),sin=Math.sin(rotation);
  const cameraDistance=span*1.35/zoom,near=span*0.015,far=span*20,focal=cameraDistance*0.85;
  const project=(x,z,y=0)=>{
    const dx=x-centerX,dz=z-centerZ,dy=y-centerY;
    const rx=dx*cos-dz*sin,rz=dx*sin+dz*cos;
    const depth=cameraDistance+rz*Math.cos(pitch)-dy*Math.sin(pitch);
    const safeDepth=Math.max(near,depth);
    const vertical=dy*Math.cos(pitch)+rz*Math.sin(pitch);
    let px=rx*focal/safeDepth*2/span,py=vertical*focal/safeDepth*2/span;
    if(aspect>1)py/=aspect;else px*=aspect;
    const depthNdc=(far+near)/(far-near)-(2*far*near)/((far-near)*safeDepth);
    return [px,py,depthNdc];
  };
  resizeCanvas(); gl.viewport(0,0,canvasRef.width,canvasRef.height);
  gl.clearColor(0.07,0.09,0.12,1);gl.clear(gl.COLOR_BUFFER_BIT|gl.DEPTH_BUFFER_BIT);
  gl.enable(gl.DEPTH_TEST);gl.useProgram(terrainProgram);
  const vao=gl.createVertexArray();gl.bindVertexArray(vao);
  const vb=gl.createBuffer();gl.bindBuffer(gl.ARRAY_BUFFER,vb);
  const p=gl.getAttribLocation(terrainProgram,"position"),uv=gl.getAttribLocation(terrainProgram,"uv");
  gl.enableVertexAttribArray(p);gl.enableVertexAttribArray(uv);
  const sampler=gl.getUniformLocation(terrainProgram,"terrainTexture");
  const hasTexture=gl.getUniformLocation(terrainProgram,"hasTexture");
  for(const patch of all){
    const packed=new Float32Array((patch.vertices.length/5)*5);
    for(let i=0,j=0;i<patch.vertices.length;i+=5,j+=5){
      const q=project(patch.vertices[i],patch.vertices[i+2],patch.vertices[i+1]);
      packed[j]=q[0];packed[j+1]=q[1];packed[j+2]=q[2];
      packed[j+3]=patch.vertices[i+3];packed[j+4]=patch.vertices[i+4];
    }
    gl.bufferData(gl.ARRAY_BUFFER,packed,gl.STREAM_DRAW);
    gl.vertexAttribPointer(p,3,gl.FLOAT,false,20,0);gl.vertexAttribPointer(uv,2,gl.FLOAT,false,20,12);
    const ib=gl.createBuffer();gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER,ib);
    gl.bufferData(gl.ELEMENT_ARRAY_BUFFER,new Uint32Array(patch.indices),gl.STREAM_DRAW);
    const valid=patch.textureIndex>=0&&terrainTextures[patch.textureIndex];
    gl.uniform1i(hasTexture,valid?1:0);
    if(valid){gl.activeTexture(gl.TEXTURE0);gl.bindTexture(gl.TEXTURE_2D,terrainTextures[patch.textureIndex]);gl.uniform1i(sampler,0);}
    gl.drawElements(gl.TRIANGLES,patch.indices.length,gl.UNSIGNED_INT,0);
    gl.deleteBuffer(ib);
  }
  gl.bindVertexArray(null);
  gl.deleteBuffer(vb);
  gl.deleteVertexArray(vao);
  if(storedTrackMesh && storedTrackMesh.vertices && storedTrackMesh.indices) drawTrackTriangles(storedTrackMesh,project);
  if(trackLines.length) drawTrackOverlay(trackLines,project);
}
function drawTrackOverlay(trackLines,project){
  const packed=new Float32Array(trackLines.length/3*6);
  for(let i=0;i<trackLines.length/3;i++){
    const q=project(trackLines[i*3],trackLines[i*3+2],trackLines[i*3+1]);
    packed[i*6]=q[0];packed[i*6+1]=q[1];packed[i*6+2]=q[2]-0.0002;
    packed[i*6+3]=0.9;packed[i*6+4]=0.9;packed[i*6+5]=0.92;
  }
  // Existing color program accepts the same position/color layout.
  gl.useProgram(program);const vao=gl.createVertexArray();gl.bindVertexArray(vao);
  const b=gl.createBuffer();gl.bindBuffer(gl.ARRAY_BUFFER,b);gl.bufferData(gl.ARRAY_BUFFER,packed,gl.STREAM_DRAW);
  const p=gl.getAttribLocation(program,"position"),c=gl.getAttribLocation(program,"color");
  gl.enableVertexAttribArray(p);gl.vertexAttribPointer(p,3,gl.FLOAT,false,24,0);
  gl.enableVertexAttribArray(c);gl.vertexAttribPointer(c,3,gl.FLOAT,false,24,12);
  gl.drawArrays(gl.LINES,0,trackLines.length/3);gl.bindVertexArray(null);
  gl.deleteBuffer(b);gl.deleteVertexArray(vao);
}

function drawTrackTriangles(mesh,project){
  if(!mesh.vertices.length || !mesh.indices.length) return;
  const packed=new Float32Array(mesh.vertices.length/3*6);
  for(let i=0;i<mesh.vertices.length;i+=3){
    const q=project(mesh.vertices[i],mesh.vertices[i+2],mesh.vertices[i+1]);
    const o=(i/3)*6;
    packed[o]=q[0];packed[o+1]=q[1];packed[o+2]=q[2]-0.0002;
    packed[o+3]=0.38;packed[o+4]=0.40;packed[o+5]=0.43;
  }
  gl.useProgram(program);
  const vao=gl.createVertexArray();gl.bindVertexArray(vao);
  const vb=gl.createBuffer();gl.bindBuffer(gl.ARRAY_BUFFER,vb);gl.bufferData(gl.ARRAY_BUFFER,packed,gl.STREAM_DRAW);
  const p=gl.getAttribLocation(program,"position"),color=gl.getAttribLocation(program,"color");
  gl.enableVertexAttribArray(p);gl.vertexAttribPointer(p,3,gl.FLOAT,false,24,0);
  gl.enableVertexAttribArray(color);gl.vertexAttribPointer(color,3,gl.FLOAT,false,24,12);
  const ib=gl.createBuffer();gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER,ib);
  gl.bufferData(gl.ELEMENT_ARRAY_BUFFER,new Uint32Array(mesh.indices),gl.STREAM_DRAW);
  gl.drawElements(gl.TRIANGLES,mesh.indices.length,gl.UNSIGNED_INT,0);
  gl.deleteBuffer(vb);gl.deleteBuffer(ib);gl.deleteVertexArray(vao);gl.bindVertexArray(null);
}
