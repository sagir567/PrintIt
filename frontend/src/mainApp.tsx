import React, { useState, useEffect, useMemo } from 'react'
import ReactDOM from 'react-dom/client'
import {
  BrowserRouter,
  Routes,
  Route,
  Navigate,
} from 'react-router-dom'
import './style.css'
import * as THREE from 'three'
import { STLLoader } from 'three/examples/jsm/loaders/STLLoader.js'
import { StlViewer } from './StlViewer'
import { CartProvider, useCart } from './CartContext'
import { AdminMaterialsPage, AdminColorsPage } from './AdminPages'

function AppShell(props: { children: React.ReactNode }) {
  return (
    <div className="app-shell">
      <header className="app-header">
        <div className="logo">PrintIt</div>
        <nav className="nav">
          <a href="/">Home</a>
          <a href="/products">Products</a>
          <a href="/upload">Upload STL</a>
          <a href="/cart">Cart</a>
          <a href="/admin/materials">Admin</a>
        </nav>
      </header>
      <main className="app-main">{props.children}</main>
      <footer className="app-footer">
        <p>© {new Date().getFullYear()} PrintIt 3D Printing Store</p>
      </footer>
    </div>
  )
}

function HomePage() {
  return (
    <div className="page home-page">
      <section className="hero-section">
        <div>
          <h1>Your local 3D printing studio</h1>
          <p>
            Upload your models, choose materials and finishes, and let us
            handle the rest. Fast, reliable prints for makers and businesses.
          </p>
          <div className="hero-actions">
            <a className="btn primary" href="/upload">
              Upload for print
            </a>
            <a className="btn secondary" href="/products">
              Browse sample products
            </a>
          </div>
        </div>
      </section>

      <section className="info-grid">
        <div className="info-card">
          <h2>How it works</h2>
          <ol>
            <li>Upload your STL file or choose a product.</li>
            <li>We review printability and pricing.</li>
            <li>We print, you pick up or receive delivery.</li>
          </ol>
        </div>
        <div className="info-card">
          <h2>Materials & colors</h2>
          <p>
            PLA, PETG, ABS and more. A wide range of standard colors, plus
            special finishes on request.
          </p>
        </div>
        <div className="info-card">
          <h2>Why PrintIt?</h2>
          <p>
            Local support, clear communication, and prints tuned for real-world
            use — not just display.
          </p>
        </div>
      </section>
    </div>
  )
}

type FilamentOption = {
  id: string
  brand: string
  materialType: { materialTypeId: string; name: string }
  color: { colorId: string; name: string; hex: string }
  inventory: { totalRemainingGrams: number; availableSpools: number }
  costPerKg?: number
}

type ProductCardProps = {
  id: string
  name: string
  priceFrom: number
  description: string
  imageUrl: string
}

const mockProducts: ProductCardProps[] = [
  {
    id: 'sample-1',
    name: 'Desk Cable Organizer',
    priceFrom: 39,
    description: 'Keep your cables tidy with a customizable desk organizer.',
    imageUrl: 'https://via.placeholder.com/400x300?text=Organizer',
  },
  {
    id: 'sample-2',
    name: 'Headphone Stand',
    priceFrom: 59,
    description: 'Minimal stand for headphones, printed in PLA or PETG.',
    imageUrl: 'https://via.placeholder.com/400x300?text=Headphone+Stand',
  },
  {
    id: 'sample-3',
    name: 'Wall Hook Set',
    priceFrom: 29,
    description: 'Strong and simple hooks for everyday use.',
    imageUrl: 'https://via.placeholder.com/400x300?text=Wall+Hooks',
  },
]

function ProductGridPage() {
  const { addItem } = useCart()

  return (
    <div className="page">
      <h1>Sample products for printing</h1>
      <p className="page-intro">
        These are example items you can order directly. For custom models, use
        the Upload STL page.
      </p>
      <div className="product-grid">
        {mockProducts.map((p) => (
          <div key={p.id} className="product-card">
            <a href={`/products/${p.id}`} className="product-card-link">
              <img src={p.imageUrl} alt={p.name} />
              <div className="product-card-body">
                <h2>{p.name}</h2>
                <p className="product-price">From ₪{p.priceFrom}</p>
                <p className="product-desc">{p.description}</p>
              </div>
            </a>
            <button
              className="btn small"
              onClick={() =>
                addItem(
                  {
                    id: p.id,
                    type: 'product',
                    name: p.name,
                    price: p.priceFrom,
                  },
                  1,
                )
              }
            >
              Add to cart
            </button>
          </div>
        ))}
      </div>
    </div>
  )
}

function ProductDetailsPage() {
  return (
    <div className="page">
      <h1>Product details</h1>
      <p>
        In a later step we will load real product data from the backend and let
        users add it to the cart.
      </p>
    </div>
  )
}

function UploadPage() {
  const [geometry, setGeometry] = useState<THREE.BufferGeometry | null>(null)
  const [fileName, setFileName] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [price, setPrice] = useState<number | null>(null)
  const [canPrint, setCanPrint] = useState<boolean | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const [filaments, setFilaments] = useState<FilamentOption[]>([])
  const [selectedMaterialId, setSelectedMaterialId] = useState<string>('')
  const [selectedColorId, setSelectedColorId] = useState<string>('')

  const selectedFilament = useMemo(() => {
    if (!selectedMaterialId || !selectedColorId) return null
    return (
      filaments.find(
        (f) =>
          f.materialType.materialTypeId === selectedMaterialId &&
          f.color.colorId === selectedColorId,
      ) || null
    )
  }, [filaments, selectedMaterialId, selectedColorId])

  const materialOptions = useMemo(() => {
    const unique = new Map<string, string>()
    filaments.forEach((f) => unique.set(f.materialType.materialTypeId, f.materialType.name))
    return Array.from(unique.entries()).map(([id, name]) => ({ id, name }))
  }, [filaments])

  const colorOptions = useMemo(() => {
    if (!selectedMaterialId) return []
    const unique = new Map<string, string>()
    filaments
      .filter((f) => f.materialType.materialTypeId === selectedMaterialId)
      .forEach((f) => unique.set(f.color.colorId, f.color.name))
    return Array.from(unique.entries()).map(([id, name]) => ({ id, name }))
  }, [filaments, selectedMaterialId])

  const estimatePrice = (geo: THREE.BufferGeometry, filament: FilamentOption | null) => {
    const box =
      geo.boundingBox ??
      new THREE.Box3().setFromBufferAttribute(
        geo.getAttribute('position') as THREE.BufferAttribute,
      )
    const size = new THREE.Vector3()
    box.getSize(size)
    const volumeApprox = size.x * size.y * size.z

    // Approximate grams: assume 20% infill, density 1.25 g/cm³ = 0.00125 g/mm³
    const density = 0.00125 // g/mm³
    const infill = 0.2
    const gramsApprox = volumeApprox * density * infill

    const basePrice = 20 // labor, etc.
    let estimatedPrice = basePrice
    if (filament && filament.costPerKg != null) {
      const materialCost = gramsApprox * (filament.costPerKg / 1000)
      estimatedPrice += materialCost
    } else {
      // Fallback to volume-based
      const volumeFactor = 0.05
      estimatedPrice += volumeFactor * volumeApprox
    }

    return Math.round(estimatedPrice)
  }

  useEffect(() => {
    fetch('http://localhost:5051/api/v1/filaments', { cache: 'no-cache' })
      .then((res) => res.json())
      .then((data) => {
        console.log('Fetched filaments:', data)
        setFilaments(data)
      })
      .catch((err) => console.error('Failed to fetch filaments', err))
  }, [])

  useEffect(() => {
    if (!geometry) return
    setPrice(estimatePrice(geometry, selectedFilament))
  }, [geometry, selectedFilament])

  async function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return

    setIsLoading(true)
    setFileName(file.name)
    setPrice(null)
    setCanPrint(null)
    setMessage(null)

    try {
      const arrayBuffer = await file.arrayBuffer()
      const loader = new STLLoader()
      const geo = loader.parse(arrayBuffer)
      geo.computeBoundingBox()

      setGeometry(geo)

      const box =
        geo.boundingBox ??
        new THREE.Box3().setFromBufferAttribute(
          geo.getAttribute('position') as THREE.BufferAttribute,
        )
      const size = new THREE.Vector3()
      box.getSize(size)

      const maxDimensionMm = 250
      const canPrintLocal =
        size.x <= maxDimensionMm &&
        size.y <= maxDimensionMm &&
        size.z <= maxDimensionMm

      setCanPrint(canPrintLocal)
      setMessage(
        canPrintLocal
          ? 'Model fits within our printer volume.'
          : 'Model is too large for our current printers.',
      )

      setPrice(estimatePrice(geo, selectedFilament))
    } catch (err) {
      console.error(err)
      setGeometry(null)
      setMessage('Could not read this STL file. Please check the file and try again.')
    } finally {
      setIsLoading(false)
    }
  }

  return (
    <div className="page">
      <h1>Upload STL for printing</h1>
      <p className="page-intro">
        Start by selecting your STL file. In the next steps we will add a 3D
        preview and live price calculation.
      </p>
      <div className="upload-panel">
        <label className="upload-dropzone">
          <span>Drag and drop STL here, or click to browse</span>
          <input type="file" accept=".stl" onChange={handleFileChange} />
        </label>
        {fileName && (
          <p className="upload-file-name">
            Selected file: <strong>{fileName}</strong>
          </p>
        )}
      </div>

      <div className="material-selection">
        <h2>Select Material & Color</h2>

        <div className="field-row">
          <div className="field">
            <label htmlFor="material-select">Material</label>
            <select
              id="material-select"
              value={selectedMaterialId}
              onChange={(e) => {
                setSelectedMaterialId(e.target.value)
                setSelectedColorId('')
              }}
            >
              <option value="">Choose material...</option>
              {materialOptions.map((m) => (
                <option key={m.id} value={m.id}>
                  {m.name}
                </option>
              ))}
            </select>
          </div>

          <div className="field">
            <label htmlFor="color-select">Color</label>
            <select
              id="color-select"
              value={selectedColorId}
              disabled={!selectedMaterialId}
              onChange={(e) => setSelectedColorId(e.target.value)}
            >
              <option value="">Choose color...</option>
              {colorOptions.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
          </div>
        </div>

        {!selectedMaterialId && <p className="status-message">Select a material to see available colors.</p>}
        {selectedMaterialId && !selectedColorId && (
          <p className="status-message">Select a color to finalize your choice.</p>
        )}
        {selectedMaterialId && selectedColorId && !selectedFilament && (
          <p className="status-error">No available filament matches this material/color combination.</p>
        )}
      </div>

      <StlViewer geometry={geometry} />

      <section className="pricing-panel">
        <h2>Estimated price</h2>
        {isLoading && <p>Analyzing model...</p>}
        {!isLoading && price == null && <p>Upload an STL file to see an estimate.</p>}
        {!isLoading && price != null && (
          <UploadQuoteSummary
            price={price}
            canPrint={canPrint}
            message={message}
            fileName={fileName}
            selectedFilament={selectedFilament}
          />
        )}
      </section>
    </div>
  )
}

type UploadQuoteSummaryProps = {
  price: number
  canPrint: boolean | null
  message: string | null
  fileName: string | null
  selectedFilament: FilamentOption | null
}

function UploadQuoteSummary({
  price,
  canPrint,
  message,
  fileName,
  selectedFilament,
}: UploadQuoteSummaryProps) {
  const { addItem } = useCart()

  const disabled = !canPrint || !selectedFilament

  return (
    <div>
      <p className="price-value">Approx. ₪{price}</p>
      {selectedFilament && (
        <p>Material: {selectedFilament.materialType.name} - {selectedFilament.color.name}</p>
      )}
      {canPrint != null && (
        <p className={canPrint ? 'status-ok' : 'status-error'}>
          {canPrint ? 'Printable' : 'Not printable with our current machines'}
        </p>
      )}
      {message && <p className="status-message">{message}</p>}
      {!selectedFilament && <p className="status-error">Please select a filament.</p>}
      <button
        className="btn primary"
        disabled={disabled}
        onClick={() =>
          addItem(
            {
              id: `stl-${Date.now()}`,
              type: 'stl',
              name: fileName ?? 'Custom STL model',
              price,
            },
            1,
          )
        }
      >
        Add this job to cart
      </button>
    </div>
  )
}

function CartPage() {
  const { items, subtotal, removeItem, updateQuantity, clear } = useCart()

  return (
    <div className="page">
      <h1>Your cart</h1>
      <p className="page-intro">
        Items you add from products or STL uploads appear here. This is a simple
        preview for the checkout flow.
      </p>
      {items.length === 0 ? (
        <p>Your cart is empty.</p>
      ) : (
        <>
          <ul className="cart-list">
            {items.map((item) => (
              <li key={item.id} className="cart-item">
                <div className="cart-item-main">
                  <h2>{item.name}</h2>
                  <p className="cart-item-meta">
                    Type: {item.type === 'product' ? 'Product' : 'Custom STL job'}
                  </p>
                </div>
                <div className="cart-item-controls">
                  <span>₪{item.price}</span>
                  <input
                    type="number"
                    min={1}
                    value={item.quantity}
                    onChange={(e) => updateQuantity(item.id, Number(e.target.value))}
                  />
                  <button
                    className="btn small secondary"
                    onClick={() => removeItem(item.id)}
                  >
                    Remove
                  </button>
                </div>
              </li>
            ))}
          </ul>
          <div className="cart-summary">
            <p>
              Subtotal: <strong>₪{subtotal.toFixed(2)}</strong>
            </p>
            <button className="btn secondary" onClick={clear}>
              Clear cart
            </button>
            <button className="btn primary" onClick={() => alert('Checkout coming soon')}>
              Proceed to checkout
            </button>
          </div>
        </>
      )}
    </div>
  )
}

function App() {
  return (
    <BrowserRouter>
      <CartProvider>
        <AppShell>
          <Routes>
            <Route path="/" element={<HomePage />} />
            <Route path="/products" element={<ProductGridPage />} />
            <Route path="/products/:id" element={<ProductDetailsPage />} />
            <Route path="/upload" element={<UploadPage />} />
            <Route path="/cart" element={<CartPage />} />
            <Route path="/admin/materials" element={<AdminMaterialsPage />} />
            <Route path="/admin/colors" element={<AdminColorsPage />} />
            <Route path="*" element={<Navigate to="/" />} />
          </Routes>
        </AppShell>
      </CartProvider>
    </BrowserRouter>
  )
}

ReactDOM.createRoot(document.getElementById('app') as HTMLElement).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
)

