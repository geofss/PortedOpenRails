let gl, program, canvasRef;

export function initialize(canvas) {
  canvasRef = canvas;
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
  if(!gl || !vertices.length || !indices.length) return;
  const count=vertices.length/3;
  let minX=Infinity,maxX=-Infinity,minZ=Infinity,maxZ=-Infinity,minY=Infinity,maxY=-Infinity;
  for(let i=0;i<count;i++){const x=vertices[i*3],y=vertices[i*3+1],z=vertices[i*3+2];minX=Math.min(minX,x);maxX=Math.max(maxX,x);minZ=Math.min(minZ,z);maxZ=Math.max(maxZ,z);minY=Math.min(minY,y);maxY=Math.max(maxY,y);}\n  for(let i=0;i<trackLines.length;i+=3){minX=Math.min(minX,trackLines[i]);maxX=Math.max(maxX,trackLines[i]);minZ=Math.min(minZ,trackLines[i+2]);maxZ=Math.max(maxZ,trackLines[i+2]);minY=Math.min(minY,trackLines[i+1]);maxY=Math.max(maxY,trackLines[i+1]);}
  const spanX=Math.max(1,maxX-minX),spanZ=Math.max(1,maxZ-minZ),span=Math.max(spanX,spanZ);
  const aspect=canvasRef.width/canvasRef.height;
  const packed=new Float32Array(count*6);
  for(let i=0;i<count;i++){
    const x=vertices[i*3],y=vertices[i*3+1],z=vertices[i*3+2];
    let px=((x-minX)/span)*1.8-0.9, py=((z-minZ)/span)*1.8-0.9;
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
  gl.bindVertexArray(null);
}
