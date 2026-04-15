import React, { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'

const API_BASE_URL = 'http://localhost:5051'

type AdminProductListItem = {
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
}

type SortOption = 'newest' | 'alphabetical' | 'active_first' | 'inactive_first'

export function AdminProductsPage() {
  const [products, setProducts] = useState<AdminProductListItem[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [search, setSearch] = useState('')
  const [sort, setSort] = useState<SortOption>('newest')

  useEffect(() => {
    const controller = new AbortController()

    const load = async () => {
      setLoading(true)
      setError(null)
      try {
        const params = new URLSearchParams()
        params.set('sort', sort)
        if (search.trim()) params.set('q', search.trim())

        const response = await fetch(`${API_BASE_URL}/api/v1/admin/products?${params.toString()}`, {
          credentials: 'include',
          signal: controller.signal,
        })

        if (!response.ok) {
          throw new Error(`Failed with status ${response.status}`)
        }

        const data = (await response.json()) as AdminProductListItem[]
        setProducts(Array.isArray(data) ? data : [])
      } catch (e) {
        if ((e as Error).name === 'AbortError') return
        setProducts([])
        setError('Could not load admin products.')
      } finally {
        setLoading(false)
      }
    }

    void load()
    return () => controller.abort()
  }, [search, sort])

  const itemCountLabel = useMemo(() => {
    if (loading) return 'Loading products…'
    if (products.length === 0) return 'No products found.'
    return `${products.length} product${products.length === 1 ? '' : 's'} found`
  }, [loading, products.length])

  return (
    <div className="admin-products-page">
      <div className="admin-products-header">
        <div>
          <h1>Products</h1>
          <p>Manage products for your current store.</p>
        </div>
        <Link className="btn primary" to="/admin/products/new">
          Add Product
        </Link>
      </div>

      <div className="admin-products-filters">
        <div className="field">
          <label htmlFor="admin-product-search">Search</label>
          <input
            id="admin-product-search"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Search by title or slug"
          />
        </div>

        <div className="field">
          <label htmlFor="admin-product-sort">Sort</label>
          <select
            id="admin-product-sort"
            value={sort}
            onChange={(e) => setSort(e.target.value as SortOption)}
          >
            <option value="newest">Newest</option>
            <option value="alphabetical">Alphabetical</option>
            <option value="active_first">Active first</option>
            <option value="inactive_first">Inactive first</option>
          </select>
        </div>
      </div>

      <p className="status-message">{itemCountLabel}</p>
      {error && <p className="status-error">{error}</p>}

      <div className="admin-products-list">
        {!loading &&
          products.map((product) => (
            <Link
              key={product.id}
              to={`/admin/products/${product.id}`}
              className="admin-product-row"
            >
              <div className="admin-product-main">
                <div className="admin-product-thumb">
                  <img
                    src={
                      product.mainImageUrl ??
                      'https://placehold.co/300x200/202530/FFFFFF?text=Product'
                    }
                    alt={product.title}
                  />
                </div>
                <div>
                  <h2>{product.title}</h2>
                  <p className="admin-product-slug">/{product.slug}</p>
                  <p className="admin-product-description">{product.description || 'No description'}</p>
                </div>
              </div>

              <div className="admin-product-meta">
                <span className={product.isActive ? 'status-ok' : 'status-error'}>
                  {product.isActive ? 'Active' : 'Inactive'}
                </span>
                <span>
                  Variants: {product.activeVariantsCount}/{product.variantsCount}
                </span>
                {product.categories.length > 0 && (
                  <span>
                    Categories:{' '}
                    {product.categories
                      .map((c) => c.name)
                      .slice(0, 3)
                      .join(', ')}
                  </span>
                )}
              </div>
            </Link>
          ))}
      </div>
    </div>
  )
}
