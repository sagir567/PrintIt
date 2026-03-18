import React, { createContext, useContext, useMemo, useState } from 'react'

export type CartItem = {
  id: string
  type: 'product' | 'stl'
  name: string
  price: number
  quantity: number
}

type CartContextValue = {
  items: CartItem[]
  addItem: (item: Omit<CartItem, 'quantity'>, quantity?: number) => void
  removeItem: (id: string) => void
  updateQuantity: (id: string, quantity: number) => void
  clear: () => void
  subtotal: number
}

const CartContext = createContext<CartContextValue | undefined>(undefined)

export function CartProvider({ children }: { children: React.ReactNode }) {
  const [items, setItems] = useState<CartItem[]>([])

  const value = useMemo<CartContextValue>(() => {
    const addItem = (item: Omit<CartItem, 'quantity'>, quantity = 1) => {
      console.log('Adding item:', item, quantity)
      setItems((prev) => {
        const existing = prev.find((i) => i.id === item.id && i.type === item.type)
        if (existing) {
          return prev.map((i) =>
            i === existing ? { ...i, quantity: i.quantity + quantity } : i,
          )
        }
        return [...prev, { ...item, quantity }]
      })
    }

    const removeItem = (id: string) => {
      setItems((prev) => prev.filter((i) => i.id !== id))
    }

    const updateQuantity = (id: string, quantity: number) => {
      setItems((prev) =>
        prev.map((i) => (i.id === id ? { ...i, quantity: Math.max(1, quantity) } : i)),
      )
    }

    const clear = () => setItems([])

    const subtotal = items.reduce((sum, i) => sum + i.price * i.quantity, 0)

    return { items, addItem, removeItem, updateQuantity, clear, subtotal }
  }, [items])

  return <CartContext.Provider value={value}>{children}</CartContext.Provider>
}

export function useCart() {
  const ctx = useContext(CartContext)
  if (!ctx) {
    throw new Error('useCart must be used within CartProvider')
  }
  return ctx
}

