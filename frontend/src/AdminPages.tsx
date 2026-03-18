import React, { useEffect, useState } from 'react'

const API_BASE_URL = 'http://localhost:5051'

type MaterialTypeItem = {
  id: string
  name: string
  isActive: boolean
}

type ColorItem = {
  id: string
  name: string
  hex: string | null
}

async function apiGet<T>(url: string): Promise<T> {
  const fullUrl = url.startsWith('http') ? url : `${API_BASE_URL}${url}`
  const resp = await fetch(fullUrl)
  if (!resp.ok) {
    throw new Error(`Request failed with status ${resp.status}`)
  }
  return resp.json()
}

export function AdminMaterialsPage() {
  const [items, setItems] = useState<MaterialTypeItem[]>([])
  const [name, setName] = useState('')
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function load() {
    setIsLoading(true)
    setError(null)
    try {
      const data = await apiGet<MaterialTypeItem[]>('/api/v1/admin/material-types')
      setItems(data)
    } catch (e) {
      setError('Could not load material types.')
    } finally {
      setIsLoading(false)
    }
  }

  useEffect(() => {
    load()
  }, [])

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault()
    if (!name.trim()) return
    setIsLoading(true)
    setError(null)
    try {
      const resp = await fetch(`${API_BASE_URL}/api/v1/admin/material-types`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name }),
      })
      if (!resp.ok) {
        throw new Error('Create failed')
      }
      setName('')
      await load()
    } catch (e) {
      setError('Could not create material type.')
    } finally {
      setIsLoading(false)
    }
  }

  async function toggleActive(id: string, isActive: boolean) {
    setIsLoading(true)
    setError(null)
    try {
      const action = isActive ? 'deactivate' : 'activate'
      const resp = await fetch(`${API_BASE_URL}/api/v1/admin/material-types/${id}/${action}`, {
        method: 'PATCH',
      })
      if (!resp.ok) {
        throw new Error('Update failed')
      }
      await load()
    } catch (e) {
      setError('Could not update material type.')
    } finally {
      setIsLoading(false)
    }
  }

  return (
    <div className="page">
      <h1>Admin – Material types</h1>
      <form className="admin-form" onSubmit={handleCreate}>
        <input
          type="text"
          placeholder="New material type name"
          value={name}
          onChange={(e) => setName(e.target.value)}
        />
        <button className="btn primary" type="submit" disabled={isLoading}>
          Add
        </button>
      </form>
      {isLoading && <p>Loading...</p>}
      {error && <p className="status-error">{error}</p>}
      <table className="admin-table">
        <thead>
          <tr>
            <th>Name</th>
            <th>Status</th>
            <th />
          </tr>
        </thead>
        <tbody>
          {items.map((m) => (
            <tr key={m.id}>
              <td>{m.name}</td>
              <td>{m.isActive ? 'Active' : 'Inactive'}</td>
              <td>
                <button
                  className="btn small"
                  onClick={() => toggleActive(m.id, m.isActive)}
                  disabled={isLoading}
                >
                  {m.isActive ? 'Deactivate' : 'Activate'}
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

export function AdminColorsPage() {
  const [items, setItems] = useState<ColorItem[]>([])
  const [name, setName] = useState('')
  const [hex, setHex] = useState('')
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function load() {
    setIsLoading(true)
    setError(null)
    try {
      const data = await apiGet<ColorItem[]>('/api/v1/admin/colors')
      setItems(data)
    } catch {
      setError('Could not load colors.')
    } finally {
      setIsLoading(false)
    }
  }

  useEffect(() => {
    load()
  }, [])

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault()
    if (!name.trim()) return
    setIsLoading(true)
    setError(null)
    try {
      const resp = await fetch(`${API_BASE_URL}/api/v1/admin/colors`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name, hex }),
      })
      if (!resp.ok) {
        throw new Error('Create failed')
      }
      setName('')
      setHex('')
      await load()
    } catch {
      setError('Could not create color.')
    } finally {
      setIsLoading(false)
    }
  }

  return (
    <div className="page">
      <h1>Admin – Colors</h1>
      <form className="admin-form" onSubmit={handleCreate}>
        <input
          type="text"
          placeholder="Color name"
          value={name}
          onChange={(e) => setName(e.target.value)}
        />
        <input
          type="text"
          placeholder="#RRGGBB (optional)"
          value={hex}
          onChange={(e) => setHex(e.target.value)}
        />
        <button className="btn primary" type="submit" disabled={isLoading}>
          Add
        </button>
      </form>
      {isLoading && <p>Loading...</p>}
      {error && <p className="status-error">{error}</p>}
      <table className="admin-table">
        <thead>
          <tr>
            <th>Name</th>
            <th>Hex</th>
          </tr>
        </thead>
        <tbody>
          {items.map((c) => (
            <tr key={c.id}>
              <td>{c.name}</td>
              <td>{c.hex ?? '—'}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

