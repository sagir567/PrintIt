import React, { useEffect, useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'

const API_BASE_URL = 'http://localhost:5051'

type AdminProductDetails = {
  id: string
  title: string
  slug: string
  description: string
  mainImageUrl: string | null
  isActive: boolean
  createdAtUtc: string
  variantsCount: number
  activeVariantsCount: number
  categories: { id: string; name: string; slug: string; isActive: boolean }[]
  variants: {
    id: string
    sizeLabel: string
    materialTypeId: string
    materialTypeName: string
    colorId: string
    colorName: string
    colorHex: string | null
    widthMm: number
    heightMm: number
    depthMm: number
    weightGrams: number
    priceOffset: number
    isActive: boolean
  }[]
}

export function AdminProductDetailsPage() {
  const { productId } = useParams()
  const [item, setItem] = useState<AdminProductDetails | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!productId) {
      setLoading(false)
      setError('Product id is missing.')
      return
    }

    const controller = new AbortController()

    const load = async () => {
      setLoading(true)
      setError(null)
      try {
        const response = await fetch(`${API_BASE_URL}/api/v1/admin/products/${productId}`, {
          credentials: 'include',
          signal: controller.signal,
        })

        if (response.status === 404) {
          setItem(null)
          setError('Product not found.')
          return
        }

        if (!response.ok) {
          throw new Error(`Failed with status ${response.status}`)
        }

        const data = (await response.json()) as AdminProductDetails
        setItem(data)
      } catch (e) {
        if ((e as Error).name === 'AbortError') return
        setItem(null)
        setError('Could not load product details.')
      } finally {
        setLoading(false)
      }
    }

    void load()
    return () => controller.abort()
  }, [productId])

  const imageUrl = useMemo(() => {
    return item?.mainImageUrl ?? 'https://placehold.co/700x450/202530/FFFFFF?text=Product'
  }, [item?.mainImageUrl])

  if (loading) {
    return (
      <div className="admin-product-details-page">
        <p>Loading product details…</p>
      </div>
    )
  }

  if (error || !item) {
    return (
      <div className="admin-product-details-page">
        <p className="status-error">{error ?? 'Product not found.'}</p>
        <Link className="btn secondary" to="/admin/products">
          Back to Products
        </Link>
      </div>
    )
  }

  return (
    <div className="admin-product-details-page">
      <div className="admin-product-details-header">
        <div>
          <h1>{item.title}</h1>
          <p className="admin-product-slug">/{item.slug}</p>
        </div>
        <div className="admin-product-details-actions">
          <span className={item.isActive ? 'status-ok' : 'status-error'}>
            {item.isActive ? 'Active' : 'Inactive'}
          </span>
          <Link className="btn secondary" to="/admin/products">
            Back to list
          </Link>
        </div>
      </div>

      <div className="admin-product-details-grid">
        <section className="admin-panel-card">
          <img className="admin-product-details-image" src={imageUrl} alt={item.title} />
          <p style={{ marginTop: 10 }}>{item.description || 'No description.'}</p>
        </section>

        <section className="admin-panel-card">
          <h2>Product summary</h2>
          <p>
            <strong>Created:</strong> {new Date(item.createdAtUtc).toLocaleString()}
          </p>
          <p>
            <strong>Variants:</strong> {item.activeVariantsCount}/{item.variantsCount} active
          </p>

          <div style={{ marginTop: 10 }}>
            <strong>Categories</strong>
            {item.categories.length === 0 ? (
              <p className="status-message">No categories assigned.</p>
            ) : (
              <div className="pill-row" style={{ marginTop: 6 }}>
                {item.categories.map((c) => (
                  <span key={c.id} className="pill">
                    {c.name}
                  </span>
                ))}
              </div>
            )}
          </div>
        </section>
      </div>

      <section className="admin-panel-card">
        <h2>Variants</h2>
        {item.variants.length === 0 ? (
          <p className="status-message">No variants configured.</p>
        ) : (
          <div className="admin-variant-list">
            {item.variants.map((variant) => (
              <div key={variant.id} className="admin-variant-item">
                <div>
                  <strong>
                    {variant.sizeLabel} · {variant.materialTypeName} · {variant.colorName}
                  </strong>
                  <p className="status-message">
                    {variant.widthMm}×{variant.heightMm}×{variant.depthMm} mm · {variant.weightGrams} g
                  </p>
                </div>
                <div style={{ textAlign: 'right' }}>
                  <span className={variant.isActive ? 'status-ok' : 'status-error'}>
                    {variant.isActive ? 'Active' : 'Inactive'}
                  </span>
                  <p className="status-message">Price offset: ₪{Number(variant.priceOffset).toFixed(2)}</p>
                </div>
              </div>
            ))}
          </div>
        )}
      </section>

      <section className="admin-panel-card">
        <h2>Product stats (placeholder)</h2>
        <p>
          Real sales/profit analytics are not wired in Phase 1 yet. This section is intentionally a
          placeholder.
        </p>
      </section>
    </div>
  )
}
