import React, { createContext, useContext, useEffect, useMemo, useState } from 'react'
import ReactDOM from 'react-dom/client'
import {
  BrowserRouter,
  NavLink,
  Outlet,
  Routes,
  Route,
  Navigate,
  Link,
  useParams,
} from 'react-router-dom'
import './style.css'
import * as THREE from 'three'
import { STLLoader } from 'three/examples/jsm/loaders/STLLoader.js'
import { StlViewer } from './StlViewer'
import { CartProvider, useCart } from './CartContext'

const API_BASE_URL = 'http://localhost:5051'

type AdminUser = {
  id: string
  email: string
}

type AdminAuthState = {
  status: 'loading' | 'authenticated' | 'anonymous'
  admin: AdminUser | null
}

type AdminAuthContextValue = AdminAuthState & {
  refresh: () => Promise<void>
  login: (email: string, password: string) => Promise<{ ok: boolean; error?: string }>
  logout: () => Promise<void>
}

const AdminAuthContext = createContext<AdminAuthContextValue | undefined>(undefined)

function AdminAuthProvider(props: { children: React.ReactNode }) {
  const [state, setState] = useState<AdminAuthState>({ status: 'loading', admin: null })

  const refresh = async () => {
    try {
      const response = await fetch(`${API_BASE_URL}/api/v1/admin/auth/me`, {
        credentials: 'include',
      })

      if (response.status === 401) {
        setState({ status: 'anonymous', admin: null })
        return
      }

      if (!response.ok) {
        setState({ status: 'anonymous', admin: null })
        return
      }

      const me = (await response.json()) as AdminUser
      setState({ status: 'authenticated', admin: me })
    } catch {
      setState({ status: 'anonymous', admin: null })
    }
  }

  useEffect(() => {
    void refresh()
  }, [])

  const login = async (email: string, password: string) => {
    try {
      const response = await fetch(`${API_BASE_URL}/api/v1/admin/auth/login`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, password }),
      })

      if (response.status === 401) {
        setState({ status: 'anonymous', admin: null })
        return { ok: false, error: 'Invalid email or password.' }
      }

      if (!response.ok) {
        setState({ status: 'anonymous', admin: null })
        return { ok: false, error: 'Login failed. Please try again.' }
      }

      await refresh()
      return { ok: true }
    } catch {
      setState({ status: 'anonymous', admin: null })
      return { ok: false, error: 'Network error. Please try again.' }
    }
  }

  const logout = async () => {
    try {
      await fetch(`${API_BASE_URL}/api/v1/admin/auth/logout`, {
        method: 'POST',
        credentials: 'include',
      })
    } finally {
      setState({ status: 'anonymous', admin: null })
    }
  }

  const value = useMemo<AdminAuthContextValue>(
    () => ({
      ...state,
      refresh,
      login,
      logout,
    }),
    [state],
  )

  return <AdminAuthContext.Provider value={value}>{props.children}</AdminAuthContext.Provider>
}

function useAdminAuth() {
  const ctx = useContext(AdminAuthContext)
  if (!ctx) throw new Error('useAdminAuth must be used inside AdminAuthProvider')
  return ctx
}

function ProtectedAdminRoute(props: { children: React.ReactNode }) {
  const auth = useAdminAuth()

  if (auth.status === 'loading') {
    return (
      <div className="page">
        <h1>Checking admin session…</h1>
        <p>Please wait.</p>
      </div>
    )
  }

  if (auth.status !== 'authenticated') {
    return <Navigate to="/admin/login" replace />
  }

  return <>{props.children}</>
}

function AdminLoginPage() {
  const auth = useAdminAuth()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  if (auth.status === 'authenticated') {
    return <Navigate to="/admin/dashboard" replace />
  }

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    setIsSubmitting(true)

    const result = await auth.login(email.trim(), password)
    if (!result.ok) {
      setError(result.error ?? 'Login failed')
    }

    setIsSubmitting(false)
  }

  return (
    <div className="page admin-login-page">
      <div className="admin-login-card">
        <h1>Admin login</h1>
        <p>Sign in to access the protected admin area.</p>

        <form className="admin-login-form" onSubmit={onSubmit}>
          <label>
            Email
            <input
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              autoComplete="username"
              required
            />
          </label>

          <label>
            Password
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              autoComplete="current-password"
              required
            />
          </label>

          {error && <p className="status-error">{error}</p>}

          <button className="btn primary" type="submit" disabled={isSubmitting}>
            {isSubmitting ? 'Signing in…' : 'Sign in'}
          </button>
        </form>
      </div>
    </div>
  )
}

function AdminShellLayout() {
  const auth = useAdminAuth()

  return (
    <div className="admin-shell">
      <aside className="admin-sidebar">
        <h2>Admin</h2>
        <nav>
          <NavLink to="/admin/dashboard">Dashboard</NavLink>
          <NavLink to="/admin/products">Products</NavLink>
          <NavLink to="/admin/inventory">Inventory</NavLink>
          <NavLink to="/admin/orders">Orders</NavLink>
          <NavLink to="/admin/alerts">Alerts</NavLink>
        </nav>
      </aside>

      <section className="admin-content">
        <div className="admin-toolbar">
          <div>
            <strong>{auth.admin?.email}</strong>
          </div>
          <button className="btn secondary" onClick={() => void auth.logout()}>
            Logout
          </button>
        </div>

        <Outlet />
      </section>
    </div>
  )
}

function AdminPlaceholderPage(props: { title: string; description: string }) {
  return (
    <div className="admin-panel-card">
      <h1>{props.title}</h1>
      <p>{props.description}</p>
    </div>
  )
}

function AppShell(props: { children: React.ReactNode }) {
  return (
    <div className="app-shell">
      <header className="app-header">
        <div className="logo">PrintIt</div>
        <nav className="nav">
          <Link to="/">Home</Link>
          <Link to="/products">Products</Link>
          <Link to="/upload">Upload STL</Link>
          <Link to="/cart">Cart</Link>
          <Link to="/admin">Admin</Link>
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
  title: string
  slug: string
  priceFrom: number
  description: string
  imageUrl: string | null
  categories: { id: string; name: string; slug: string }[]
}

function ProductGridPage() {
  const [products, setProducts] = useState<ProductCardProps[]>([])
  const [categories, setCategories] = useState<{ id: string; name: string; slug: string }[]>([])
  const [selectedCategory, setSelectedCategory] = useState('')
  const [search, setSearch] = useState('')
  const [sort, setSort] = useState('newest')
  const [loading, setLoading] = useState(false)

  useEffect(() => {
    fetch(`${API_BASE_URL}/api/v1/catalog/categories`)
      .then((res) => res.json())
      .then((data) => setCategories(Array.isArray(data) ? data : []))
      .catch((err) => console.error('Failed to load categories', err))
  }, [])

  useEffect(() => {
    const q = new URLSearchParams()
    q.set('sort', sort)
    q.set('page', '1')
    q.set('pageSize', '50')
    if (selectedCategory) q.set('category', selectedCategory)
    if (search.trim()) q.set('q', search.trim())

    setLoading(true)
    fetch(`${API_BASE_URL}/api/v1/catalog/products?${q.toString()}`)
      .then((res) => res.json())
      .then((data) => {
        const items = Array.isArray(data?.items) ? data.items : []
        const mapped: ProductCardProps[] = items.map((p: any) => ({
          id: p.id,
          title: p.title,
          slug: p.slug,
          priceFrom: p.priceFrom,
          description: p.description ?? '',
          imageUrl: p.mainImageUrl ?? null,
          categories: Array.isArray(p.categories) ? p.categories : [],
        }))
        setProducts(mapped)
      })
      .catch((err) => {
        console.error('Failed to load products', err)
        setProducts([])
      })
      .finally(() => setLoading(false))
  }, [selectedCategory, search, sort])

  return (
    <div className="page">
      <h1>Catalog products</h1>
      <p className="page-intro">
        Browse products from our database catalog. Use search, category and sort
        to find what you need.
      </p>

      <div className="field-row" style={{ marginBottom: 16 }}>
        <div className="field">
          <label htmlFor="catalog-search">Search</label>
          <input
            id="catalog-search"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Search title or description..."
          />
        </div>
        <div className="field">
          <label htmlFor="catalog-category">Category</label>
          <select
            id="catalog-category"
            value={selectedCategory}
            onChange={(e) => setSelectedCategory(e.target.value)}
          >
            <option value="">All categories</option>
            {categories.map((c) => (
              <option key={c.id} value={c.slug}>
                {c.name}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="catalog-sort">Sort</label>
          <select id="catalog-sort" value={sort} onChange={(e) => setSort(e.target.value)}>
            <option value="newest">Newest</option>
            <option value="name_asc">Name A-Z</option>
            <option value="name_desc">Name Z-A</option>
            <option value="price_asc">Price low-high</option>
            <option value="price_desc">Price high-low</option>
          </select>
        </div>
      </div>

      {loading && <p>Loading catalog...</p>}
      {!loading && products.length === 0 && <p>No products found.</p>}

      <div className="product-grid">
        {products.map((p) => (
          <div key={p.id} className="product-card">
            <Link to={`/products/${p.slug}`} className="product-card-link product-card-tile">
              <div className="product-card-image-wrap">
                <img
                  src={p.imageUrl ?? 'https://placehold.co/600x400/202530/FFFFFF?text=PrintIt+Product'}
                  alt={p.title}
                />
                <div className="product-card-overlay">
                  <h2>{p.title}</h2>
                  <p className="product-price">From ₪{p.priceFrom.toFixed(2)}</p>
                  <p className="product-desc">{p.description}</p>
                  {p.categories.length > 0 && (
                    <div className="pill-row">
                      {p.categories.map((c) => (
                        <span key={c.id} className="pill">
                          {c.name}
                        </span>
                      ))}
                    </div>
                  )}
                </div>
              </div>
            </Link>
          </div>
        ))}
      </div>
    </div>
  )
}

function ProductDetailsPage() {
  const { addItem } = useCart()
  const { id } = useParams()
  const [product, setProduct] = useState<any | null>(null)
  const [loading, setLoading] = useState(true)
  const [selectedSize, setSelectedSize] = useState<string>('')
  const [selectedMaterialId, setSelectedMaterialId] = useState<string>('')
  const [selectedColorId, setSelectedColorId] = useState<string>('')

  useEffect(() => {
    if (!id) return
    setLoading(true)
    fetch(`${API_BASE_URL}/api/v1/catalog/products/${id}`)
      .then((res) => (res.ok ? res.json() : null))
      .then((data) => setProduct(data))
      .catch((err) => {
        console.error('Failed to load product details', err)
        setProduct(null)
      })
      .finally(() => setLoading(false))
  }, [id])

  useEffect(() => {
    if (!product || !Array.isArray(product.variants) || product.variants.length === 0) return
    const first = product.variants[0]
    setSelectedSize(first.sizeLabel)
    setSelectedMaterialId(first.materialType?.materialTypeId ?? '')
    setSelectedColorId(first.color?.colorId ?? '')
  }, [product])

  const variants: any[] = Array.isArray(product?.variants) ? product.variants : []
  const sizeOptions = useMemo(() => {
    const unique = new Map<string, true>()
    variants.forEach((v) => unique.set(v.sizeLabel, true))
    return Array.from(unique.keys())
  }, [variants])

  const materialOptions = useMemo(() => {
    const filtered = variants.filter((v) => (!selectedSize ? true : v.sizeLabel === selectedSize))
    const unique = new Map<string, { id: string; name: string }>()
    filtered.forEach((v) => {
      const id = v.materialType?.materialTypeId
      if (!id) return
      unique.set(id, { id, name: v.materialType?.name ?? 'Material' })
    })
    return Array.from(unique.values())
  }, [variants, selectedSize])

  const colorOptions = useMemo(() => {
    const filtered = variants.filter(
      (v) =>
        v.sizeLabel === selectedSize &&
        (!selectedMaterialId || v.materialType?.materialTypeId === selectedMaterialId),
    )
    const unique = new Map<string, { id: string; name: string; hex?: string }>()
    filtered.forEach((v) => {
      const id = v.color?.colorId
      if (!id) return
      unique.set(id, { id, name: v.color?.name ?? 'Color', hex: v.color?.hex })
    })
    return Array.from(unique.values())
  }, [variants, selectedSize, selectedMaterialId])

  useEffect(() => {
    if (materialOptions.length === 0) {
      setSelectedMaterialId('')
      return
    }

    if (!materialOptions.some((m) => m.id === selectedMaterialId)) {
      setSelectedMaterialId(materialOptions[0].id)
    }
  }, [materialOptions, selectedMaterialId])

  useEffect(() => {
    if (colorOptions.length === 0) {
      setSelectedColorId('')
      return
    }

    if (!colorOptions.some((c) => c.id === selectedColorId)) {
      setSelectedColorId(colorOptions[0].id)
    }
  }, [colorOptions, selectedColorId])

  const selectedVariant = useMemo(() => {
    return variants.find(
      (v) =>
        v.sizeLabel === selectedSize &&
        v.materialType?.materialTypeId === selectedMaterialId &&
        v.color?.colorId === selectedColorId,
    )
  }, [variants, selectedSize, selectedMaterialId, selectedColorId])

  const selectedVariantPrice = selectedVariant ? Number(selectedVariant.price) : null
  const selectedVariantLabel = selectedVariant
    ? `${selectedVariant.sizeLabel} · ${selectedVariant.materialType?.name} · ${selectedVariant.color?.name}`
    : null

  const displayPrice = selectedVariant ? Number(selectedVariant.price).toFixed(2) : '—'
  const heroImage = product?.mainImageUrl ?? 'https://placehold.co/800x600/202530/FFFFFF?text=PrintIt+Product'

  if (loading) {
    return (
      <div className="page">
        <h1>Product details</h1>
        <p>Loading...</p>
      </div>
    )
  }

  if (!product) {
    return (
      <div className="page">
        <h1>Product details</h1>
        <p>Product not found.</p>
      </div>
    )
  }

  return (
    <div className="page product-detail">
      <div className="product-detail-hero">
        <img src={heroImage} alt={product.title} />
      </div>
      <div className="product-detail-body">
        <div className="product-detail-header">
          <h1>{product.title}</h1>
          <p className="product-detail-price">₪{displayPrice}</p>
        </div>
        <p className="product-detail-description">{product.description}</p>

        {Array.isArray(product.categories) && product.categories.length > 0 && (
          <div className="pill-row" style={{ marginBottom: 12 }}>
            {product.categories.map((c: any) => (
              <span key={c.id} className="pill">
                {c.name}
              </span>
            ))}
          </div>
        )}

        <div className="selector-row">
          <div className="field">
            <label>Size</label>
            <select value={selectedSize} onChange={(e) => setSelectedSize(e.target.value)}>
              {sizeOptions.map((s) => (
                <option key={s} value={s}>
                  {s}
                </option>
              ))}
            </select>
          </div>

          <div className="field">
            <label>Material</label>
            <select
              value={selectedMaterialId}
              onChange={(e) => setSelectedMaterialId(e.target.value)}
              disabled={materialOptions.length === 0}
            >
              {materialOptions.map((m) => (
                <option key={m.id} value={m.id}>
                  {m.name}
                </option>
              ))}
            </select>
          </div>

          <div className="field">
            <label>Color</label>
            <select
              value={selectedColorId}
              onChange={(e) => setSelectedColorId(e.target.value)}
              disabled={colorOptions.length === 0}
            >
              {colorOptions.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
          </div>
        </div>

        <div className="variant-summary">
          {selectedVariant ? (
            <>
              <div>
                <strong>Selected:</strong> {selectedVariant.sizeLabel} · {selectedVariant.materialType?.name} ·{' '}
                {selectedVariant.color?.name}
              </div>
              <div className="variant-meta">
                {selectedVariant.widthMm}×{selectedVariant.heightMm}×{selectedVariant.depthMm} mm ·{' '}
                {selectedVariant.weightGrams} g
              </div>
              <div className="variant-price">₪{Number(selectedVariant.price).toFixed(2)}</div>
            </>
          ) : (
            <div className="status-error">No variant available for this selection.</div>
          )}
        </div>

        <div style={{ marginTop: 12 }}>
          {selectedVariantLabel && (
            <p className="status-message" style={{ marginBottom: 8 }}>
              Add to cart: {product.title} ({selectedVariantLabel})
            </p>
          )}
          <button
            className="btn primary"
            disabled={!selectedVariant || selectedVariantPrice == null}
            onClick={() => {
              if (!selectedVariant || selectedVariantPrice == null) return

              addItem(
                {
                  id: selectedVariant.id,
                  type: 'product',
                  name: `${product.title} (${selectedVariant.sizeLabel} · ${selectedVariant.materialType?.name} · ${selectedVariant.color?.name})`,
                  details: selectedVariantLabel ?? undefined,
                  imageUrl: heroImage,
                  price: selectedVariantPrice,
                },
                1,
              )
            }}
          >
            Add to cart
          </button>
        </div>

        <div className="variant-list">
          <h3>Available variants</h3>
          <ul>
            {variants.map((v) => (
              <li key={v.id}>
                {v.sizeLabel} · {v.materialType?.name} · {v.color?.name} · ₪{Number(v.price).toFixed(2)}
              </li>
            ))}
          </ul>
        </div>
      </div>
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
    fetch(`${API_BASE_URL}/api/v1/filaments`, { cache: 'no-cache' })
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
              details: selectedFilament
                ? `${selectedFilament.materialType.name} · ${selectedFilament.color.name}`
                : undefined,
              imageUrl: 'https://placehold.co/400x300/1e1e2f/FFFFFF?text=STL+Job',
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
  const total = subtotal

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
                  <div className="cart-item-thumb">
                    <img
                      src={
                        item.imageUrl ??
                        (item.type === 'product'
                          ? 'https://placehold.co/300x220/202530/FFFFFF?text=Product'
                          : 'https://placehold.co/300x220/1e1e2f/FFFFFF?text=STL+Job')
                      }
                      alt={item.name}
                    />
                  </div>
                  <div className="cart-item-info">
                    <h2>{item.name}</h2>
                    <p className="cart-item-meta">
                      {item.details ??
                        (item.type === 'product' ? 'Configured product' : 'Custom STL print job')}
                    </p>
                    <p className="cart-item-meta">Unit: ₪{item.price.toFixed(2)}</p>
                  </div>
                </div>
                <div className="cart-item-controls">
                  <label>
                    Qty
                    <input
                      type="number"
                      min={1}
                      value={item.quantity}
                      onChange={(e) => updateQuantity(item.id, Number(e.target.value))}
                    />
                  </label>
                  <span className="cart-line-total">₪{(item.price * item.quantity).toFixed(2)}</span>
                  <button className="btn small secondary" onClick={() => removeItem(item.id)}>
                    Remove
                  </button>
                </div>
              </li>
            ))}
          </ul>

          <div className="cart-summary-card">
            <div className="cart-summary-row">
              <span>Subtotal</span>
              <strong>₪{subtotal.toFixed(2)}</strong>
            </div>
            <div className="cart-summary-row">
              <span>Shipping</span>
              <span>Calculated at checkout</span>
            </div>
            <div className="cart-summary-row cart-summary-total">
              <span>Total</span>
              <strong>₪{total.toFixed(2)}</strong>
            </div>

            <div className="cart-summary-actions">
              <button className="btn secondary" onClick={clear}>
                Clear cart
              </button>
              <button className="btn primary" onClick={() => alert('Checkout is not implemented yet')}>
                Pay now
              </button>
            </div>
          </div>
        </>
      )}
    </div>
  )
}

function App() {
  return (
    <BrowserRouter>
      <AdminAuthProvider>
        <CartProvider>
          <AppShell>
            <Routes>
              <Route path="/" element={<HomePage />} />
              <Route path="/products" element={<ProductGridPage />} />
              <Route path="/products/:id" element={<ProductDetailsPage />} />
              <Route path="/upload" element={<UploadPage />} />
              <Route path="/cart" element={<CartPage />} />

              <Route path="/admin/login" element={<AdminLoginPage />} />
              <Route
                path="/admin"
                element={
                  <ProtectedAdminRoute>
                    <AdminShellLayout />
                  </ProtectedAdminRoute>
                }
              >
                <Route index element={<Navigate to="dashboard" replace />} />
                <Route
                  path="dashboard"
                  element={
                    <AdminPlaceholderPage
                      title="Dashboard"
                      description="Admin dashboard foundation is ready. Dashboard widgets will be added in the next phase."
                    />
                  }
                />
                <Route
                  path="products"
                  element={
                    <AdminPlaceholderPage
                      title="Products"
                      description="Product management module placeholder. Product create/edit tools will be added in a later phase."
                    />
                  }
                />
                <Route
                  path="inventory"
                  element={
                    <AdminPlaceholderPage
                      title="Inventory"
                      description="Inventory management module placeholder. Stock and spool workflows will be wired here later."
                    />
                  }
                />
                <Route
                  path="orders"
                  element={
                    <AdminPlaceholderPage
                      title="Orders"
                      description="Orders module placeholder. Viewing and status update flows will be added in the next phase."
                    />
                  }
                />
                <Route
                  path="alerts"
                  element={
                    <AdminPlaceholderPage
                      title="Alerts"
                      description="Low-stock and reorder alerts placeholder. Alert logic will be added later."
                    />
                  }
                />
              </Route>

              <Route path="*" element={<Navigate to="/" />} />
            </Routes>
          </AppShell>
        </CartProvider>
      </AdminAuthProvider>
    </BrowserRouter>
  )
}

ReactDOM.createRoot(document.getElementById('app') as HTMLElement).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
)








