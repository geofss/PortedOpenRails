let gl, program, canvasRef, storedVertices, storedIndices, storedTrackLines;
let rotation = 0, zoom = 1, dragging = false, lastX = 0;

export function initialize(canvas) {
  canvasRef = canvas;
  resizeCanvas();
  canvas.addEventListener('wheel', e => { e.preventDefault(); zoom = Math.max(0.35, Math.min(5, zoom * (e.deltaY < 0 ? 1.12 : 0.89))); redraw(); }, { passive:false });
  canvas.addEventListener('pointerdown', e => { dragging=true; lastX=e.clientX; canvas.setPointerCapture(e.pointerId); });
  canvas.addEventListener('pointermove', e => { if(dragging){ rotation += (e.clientX-lastX)*0.008; lastX=e.clientX; redraw(); } });
  canvas.addEventListener('pointerup', () => dragging=false);
  canvas.addEventListener('pointercancel', () => dragging=false);
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
function compile(type,source){const s=gl.createShader(type);gl.shaderSource(s,source);gl.compileShader(s);if(!gl.getShaderParameter(s,gl.COMPILE_STATUS))throw new Error(gl.getShaderInfoLog(s));return s;}

export function drawTerrain(vertices, indices, trackLines = []) {
  storedVertices=vertices; storedIndices=indices; storedTrackLines=trackLines; redraw();
}
function resizeCanvas(){ const dpr=window.devicePixelRatio||1; const w=Math.max(1,Math.floor(canvasRef.clientWidth*dpr)),h=Math.max(1,Math.floor(canvasRef.clientHeight*dpr)); if(canvasRef.width!==w||canvasRef.height!==h){canvasRef.width=w;canvasRef.height=h;}}
function redraw(){
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
  }
  gl.bindVertexArray(null);
}
