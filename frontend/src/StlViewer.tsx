import React, { Suspense, useMemo } from 'react'
import { Canvas } from '@react-three/fiber'
import { OrbitControls } from '@react-three/drei'
import * as THREE from 'three'

type StlViewerProps = {
  geometry: THREE.BufferGeometry | null
}

function StlMesh({ geometry }: { geometry: THREE.BufferGeometry }) {
  const material = useMemo(
    () => new THREE.MeshStandardMaterial({ color: '#ff6600', metalness: 0.1, roughness: 0.6 }),
    [],
  )

  const box = useMemo(() => new THREE.Box3().setFromBufferAttribute(geometry.getAttribute('position') as THREE.BufferAttribute), [geometry])
  const size = useMemo(() => {
    const s = new THREE.Vector3()
    box.getSize(s)
    return s
  }, [box])

  const maxDimension = Math.max(size.x, size.y, size.z) || 1
  const scaleFactor = 10 / maxDimension

  return (
    <mesh geometry={geometry} material={material} scale={scaleFactor}>
      <ambientLight intensity={0.6} />
      <directionalLight position={[5, 10, 5]} intensity={0.8} />
    </mesh>
  )
}

export function StlViewer({ geometry }: StlViewerProps) {
  if (!geometry) {
    return (
      <div className="stl-viewer empty">
        <p>Select an STL file to see a 3D preview here.</p>
      </div>
    )
  }

  return (
    <div className="stl-viewer">
      <Canvas camera={{ position: [0, 0, 20], fov: 35 }}>
        <color attach="background" args={['#0b1020']} />
        <Suspense fallback={null}>
          <StlMesh geometry={geometry} />
        </Suspense>
        <OrbitControls enablePan enableZoom enableRotate />
      </Canvas>
    </div>
  )
}

