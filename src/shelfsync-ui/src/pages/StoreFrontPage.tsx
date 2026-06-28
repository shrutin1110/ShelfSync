import { useState, useEffect } from 'react';
import { GraphQLClient } from 'graphql-request';
import axios from 'axios';

const AUTH_API = (import.meta.env.VITE_AUTH_API ?? 'http://localhost:5000') as string;
const GRAPHQL_API = (import.meta.env.VITE_GRAPHQL_API ?? 'http://localhost:5002/graphql') as string;

const GET_PRODUCTS = `
  query {
    products {
      id
      name
      sku
      price
      stockQuantity
      isActive
    }
  }
`;

const PLACE_ORDER = `
  mutation PlaceOrder($input: PlaceOrderInput!) {
    placeOrder(input: $input) {
      success
      errorMessage
      orderId
    }
  }
`;

interface Tenant {
    id: string;
    name: string;
}

interface Product {
    id: string;
    name: string;
    sku: string;
    price: number;
    stockQuantity: number;
    isActive: boolean;
}

interface CartItem {
    productId: string;
    productName: string;
    price: number;
    quantity: number;
}

type Step = 'select-tenant' | 'browse' | 'success';

export default function StorefrontPage() {

    const [step, setStep] = useState<Step>('select-tenant');
    const [tenants, setTenants] = useState<Tenant[]>([]);
    const [selectedTenant, setSelectedTenant] = useState<Tenant | null>(null);
    const [storefrontToken, setStorefrontToken] = useState<string | null>(null);
    const [products, setProducts] = useState<Product[]>([]);
    const [cart, setCart] = useState<CartItem[]>([]);
    const [customerName, setCustomerName] = useState('');
    const [notes, setNotes] = useState('');
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [confirmedOrderId, setConfirmedOrderId] = useState<string | null>(null);

    useEffect(() => {
        axios.get(`${AUTH_API}/api/auth/tenants`)
            .then(res => setTenants(res.data))
            .catch(err => {
                console.error('Failed to load tenants:', err);
                setError('Failed to load stores. Make sure Auth service is running.');
            });
    }, []);

    const handleSelectTenant = async (tenant: Tenant) => {
        setLoading(true);
        setError(null);

        try {
            const tokenResponse = await axios.post(
                `${AUTH_API}/api/auth/storefront-token`,
                { tenantId: tenant.id }
            );

            const token = tokenResponse.data.accessToken;
            setStorefrontToken(token);
            setSelectedTenant(tenant);

            const client = new GraphQLClient(GRAPHQL_API, {
                headers: { Authorization: `Bearer ${token}` }
            });

            const result = await client
                .request<{ products: Product[] }>(GET_PRODUCTS);

            setProducts(result.products.filter(
                p => p.isActive && p.stockQuantity > 0
            ));

            setStep('browse');
        } catch (err: any) {
            setError(err.response?.data?.message ?? 'Failed to load store. Please try again.');
        } finally {
            setLoading(false);
        }
    };

    const addToCart = (product: Product) => {
        setCart(prev => {
            const existing = prev.find(item => item.productId === product.id);
            if (existing) {
                return prev.map(item =>
                    item.productId === product.id
                        ? { ...item, quantity: item.quantity + 1 }
                        : item
                );
            }
            return [...prev, {
                productId: product.id,
                productName: product.name,
                price: product.price,
                quantity: 1
            }];
        });
    };

    const updateQuantity = (productId: string, quantity: number) => {
        if (quantity <= 0) {
            setCart(prev => prev.filter(item => item.productId !== productId));
        } else {
            setCart(prev => prev.map(item =>
                item.productId === productId ? { ...item, quantity } : item
            ));
        }
    };

    const getCartQty = (productId: string) =>
        cart.find(item => item.productId === productId)?.quantity ?? 0;

    const cartTotal = cart.reduce(
        (sum, item) => sum + item.price * item.quantity, 0
    );

    const handlePlaceOrder = async () => {
        if (cart.length === 0) {
            setError('Add at least one item to your cart.');
            return;
        }
        if (!customerName.trim()) {
            setError('Please enter your name.');
            return;
        }

        setLoading(true);
        setError(null);

        try {
            const client = new GraphQLClient(GRAPHQL_API, {
                headers: { Authorization: `Bearer ${storefrontToken}` }
            });

            const result = await client.request<{
                placeOrder: {
                    success: boolean;
                    errorMessage: string | null;
                    orderId: string | null;
                }
            }>(PLACE_ORDER, {
                input: {
                    items: cart.map(item => ({
                        productId: item.productId,
                        quantity: item.quantity
                    })),
                    notes: `Customer: ${customerName}` + (notes ? `. ${notes}` : '')
                }
            });

            if (result.placeOrder.success) {
                setConfirmedOrderId(result.placeOrder.orderId);
                setCart([]);
                setStep('success');
            } else {
                setError(result.placeOrder.errorMessage ?? 'Failed to place order.');
            }
        } catch (err: any) {
            setError(err?.message ?? 'Failed to place order.');
        } finally {
            setLoading(false);
        }
    };

    return (
        <div style={styles.page}>

            {/* Header */}
            <div style={styles.header}>
                <div style={styles.headerContent}>
                    <span style={styles.logo}>🛍️</span>
                    <div>
                        <h1 style={styles.title}>ShelfSync Storefront</h1>
                        <p style={styles.headerSubtitle}>Customer Order Simulator</p>
                    </div>
                    {selectedTenant && (
                        <div style={styles.shoppingAt}>
                            Shopping at <strong>{selectedTenant.name}</strong>
                            <button
                                onClick={() => {
                                    setStep('select-tenant');
                                    setSelectedTenant(null);
                                    setCart([]);
                                    setStorefrontToken(null);
                                }}
                                style={styles.changeTenantBtn}
                            >
                                Change Store
                            </button>
                        </div>
                    )}
                </div>
            </div>

            <div style={styles.content}>

                {error && (
                    <div style={styles.error}>
                        {error}
                        <button onClick={() => setError(null)} style={styles.dismissError}>
                            x
                        </button>
                    </div>
                )}

                {/* STEP 1: Select Tenant */}
                {step === 'select-tenant' && (
                    <div style={styles.tenantSelector}>
                        <h2 style={styles.stepTitle}>Which store would you like to shop at?</h2>
                        <p style={styles.stepSubtitle}>
                            Each store is a separate business using ShelfSync to manage their orders.
                        </p>

                        {tenants.length === 0 ? (
                            <div style={styles.noTenants}>
                                No stores available yet. Register a business first at the admin portal.
                            </div>
                        ) : (
                            <div style={styles.tenantGrid}>
                                {tenants.map(tenant => (
                                    <button
                                        key={tenant.id}
                                        onClick={() => handleSelectTenant(tenant)}
                                        disabled={loading}
                                        style={styles.tenantCard}
                                    >
                                        <div style={styles.tenantIcon}>🏪</div>
                                        <div style={styles.tenantName}>{tenant.name}</div>
                                        <div style={styles.tenantShop}>Shop now</div>
                                    </button>
                                ))}
                            </div>
                        )}

                        {loading && (
                            <div style={styles.loadingMsg}>Loading store...</div>
                        )}

                        <div style={styles.demoNote}>
                            <strong>Demo Note:</strong> This page simulates a customer-facing
                            storefront. In production, orders would flow in automatically from
                            the business's own website or app via API integration.
                        </div>
                    </div>
                )}

                {/* STEP 2: Browse Products */}
                {step === 'browse' && (
                    <div style={styles.shopLayout}>

                        <div style={styles.productsSection}>
                            <h2 style={styles.stepTitle}>{selectedTenant?.name} Products</h2>

                            {products.length === 0 ? (
                                <div style={styles.noProducts}>
                                    No products available yet. Ask the admin to add products first.
                                </div>
                            ) : (
                                <div style={styles.productsGrid}>
                                    {products.map(product => (
                                        <div key={product.id} style={styles.productCard}>
                                            <div style={styles.productImage}>📦</div>
                                            <div style={styles.productInfo}>
                                                <div style={styles.productName}>{product.name}</div>
                                                <div style={styles.productSku}>SKU: {product.sku}</div>
                                                <div style={styles.productPrice}>
                                                    ${product.price.toFixed(2)}
                                                </div>
                                                <div style={styles.productStock}>
                                                    {product.stockQuantity > 10
                                                        ? 'In stock'
                                                        : `Only ${product.stockQuantity} left`}
                                                </div>
                                            </div>

                                            {getCartQty(product.id) === 0 ? (
                                                <button
                                                    onClick={() => addToCart(product)}
                                                    style={styles.addToCartBtn}
                                                >
                                                    Add to Cart
                                                </button>
                                            ) : (
                                                <div style={styles.qtyControl}>
                                                    <button
                                                        onClick={() => updateQuantity(
                                                            product.id,
                                                            getCartQty(product.id) - 1
                                                        )}
                                                        style={styles.qtyBtn}
                                                    >
                                                        -
                                                    </button>
                                                    <span style={styles.qtyNum}>
                                                        {getCartQty(product.id)}
                                                    </span>
                                                    <button
                                                        onClick={() => updateQuantity(
                                                            product.id,
                                                            getCartQty(product.id) + 1
                                                        )}
                                                        style={styles.qtyBtn}
                                                        disabled={
                                                            getCartQty(product.id) >= product.stockQuantity
                                                        }
                                                    >
                                                        +
                                                    </button>
                                                </div>
                                            )}
                                        </div>
                                    ))}
                                </div>
                            )}
                        </div>

                        {/* Cart */}
                        <div style={styles.cartSection}>
                            <h2 style={styles.stepTitle}>Your Cart</h2>

                            {cart.length === 0 ? (
                                <div style={styles.emptyCart}>
                                    <div style={styles.emptyCartIcon}>🛒</div>
                                    <p>Your cart is empty</p>
                                    <p style={{ fontSize: '13px' }}>Add products from the left</p>
                                </div>
                            ) : (
                                <div style={styles.cartContent}>
                                    {cart.map(item => (
                                        <div key={item.productId} style={styles.cartItem}>
                                            <div style={styles.cartItemLeft}>
                                                <div style={styles.cartItemName}>
                                                    {item.productName}
                                                </div>
                                                <div style={styles.cartItemPrice}>
                                                    ${item.price.toFixed(2)} each
                                                </div>
                                            </div>
                                            <div style={styles.cartItemRight}>
                                                <span style={styles.cartItemQty}>
                                                    x{item.quantity}
                                                </span>
                                                <span style={styles.cartItemTotal}>
                                                    ${(item.price * item.quantity).toFixed(2)}
                                                </span>
                                                <button
                                                    onClick={() => updateQuantity(item.productId, 0)}
                                                    style={styles.removeItem}
                                                >
                                                    x
                                                </button>
                                            </div>
                                        </div>
                                    ))}

                                    <div style={styles.cartTotal}>
                                        <span>Total</span>
                                        <span style={styles.cartTotalAmount}>
                                            ${cartTotal.toFixed(2)}
                                        </span>
                                    </div>

                                    <div style={styles.customerForm}>
                                        <label style={styles.formLabel}>Your Name *</label>
                                        <input
                                            style={styles.formInput}
                                            value={customerName}
                                            onChange={e => setCustomerName(e.target.value)}
                                            placeholder="John Smith"
                                        />
                                        <label style={styles.formLabel}>Delivery Notes</label>
                                        <input
                                            style={styles.formInput}
                                            value={notes}
                                            onChange={e => setNotes(e.target.value)}
                                            placeholder="Leave at door, gift wrap etc."
                                        />
                                    </div>

                                    <button
                                        onClick={handlePlaceOrder}
                                        disabled={loading || cart.length === 0}
                                        style={{
                                            ...styles.placeOrderBtn,
                                            opacity: loading ? 0.7 : 1
                                        }}
                                    >
                                        {loading
                                            ? 'Placing Order...'
                                            : `Place Order — $${cartTotal.toFixed(2)}`}
                                    </button>
                                </div>
                            )}
                        </div>
                    </div>
                )}

                {/* STEP 3: Success */}
                {step === 'success' && (
                    <div style={styles.successPage}>
                        <div style={styles.successCard}>
                            <div style={styles.successIcon}>✅</div>
                            <h2 style={styles.successTitle}>Order Placed!</h2>
                            <p style={styles.successSubtitle}>
                                Your order has been received by{' '}
                                <strong>{selectedTenant?.name}</strong>
                            </p>

                            <div style={styles.orderId}>
                                Order #{confirmedOrderId?.slice(0, 8).toUpperCase()}
                            </div>

                            <div style={styles.successSteps}>
                                <div style={styles.successStep}>
                                    <span style={styles.stepIcon}>📦</span>
                                    <span>Stock reserved in warehouse</span>
                                </div>
                                <div style={styles.successStep}>
                                    <span style={styles.stepIcon}>📧</span>
                                    <span>Confirmation notification sent</span>
                                </div>
                                <div style={styles.successStep}>
                                    <span style={styles.stepIcon}>🧾</span>
                                    <span>Invoice being generated</span>
                                </div>
                            </div>

                            <div style={styles.successNote}>
                                <strong>Now switch to the admin view</strong>
                                <br />
                                Login as the store admin to see this order appear
                                in the dashboard and process it through the pipeline.
                            </div>

                            <div style={styles.successActions}>
                                <button
                                    onClick={() => {
                                        setStep('browse');
                                        setConfirmedOrderId(null);
                                    }}
                                    style={styles.shopMoreBtn}
                                >
                                    Place Another Order
                                </button>
                                <a href="/login" style={styles.adminLoginBtn}>
                                    Go to Admin Login
                                </a>
                            </div>
                        </div>
                    </div>
                )}

            </div>
        </div>
    );
}

const styles: Record<string, React.CSSProperties> = {
    page: {
        minHeight: '100vh',
        backgroundColor: '#f8f9fa',
        fontFamily: '-apple-system, BlinkMacSystemFont, sans-serif'
    },
    header: {
        backgroundColor: '#1a1a2e',
        color: 'white',
        padding: '16px 32px',
        boxShadow: '0 2px 8px rgba(0,0,0,0.2)'
    },
    headerContent: {
        maxWidth: '1200px',
        margin: '0 auto',
        display: 'flex',
        alignItems: 'center',
        gap: '16px'
    },
    logo: { fontSize: '28px' },
    title: { fontSize: '20px', fontWeight: '700', margin: 0 },
    headerSubtitle: {
        fontSize: '12px',
        color: 'rgba(255,255,255,0.6)',
        margin: 0
    },
    shoppingAt: {
        marginLeft: 'auto',
        fontSize: '14px',
        color: 'rgba(255,255,255,0.8)',
        display: 'flex',
        alignItems: 'center',
        gap: '12px'
    },
    changeTenantBtn: {
        padding: '4px 10px',
        backgroundColor: 'rgba(255,255,255,0.15)',
        color: 'white',
        border: 'none',
        borderRadius: '6px',
        fontSize: '12px',
        cursor: 'pointer'
    },
    content: {
        maxWidth: '1200px',
        margin: '0 auto',
        padding: '32px'
    },
    error: {
        backgroundColor: '#fff0f0',
        border: '1px solid #ffcccc',
        borderRadius: '8px',
        padding: '12px 16px',
        color: '#cc0000',
        fontSize: '14px',
        marginBottom: '20px',
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center'
    },
    dismissError: {
        background: 'none',
        border: 'none',
        color: '#cc0000',
        fontSize: '18px',
        cursor: 'pointer',
        padding: '0 4px'
    },
    tenantSelector: {
        maxWidth: '600px',
        margin: '0 auto',
        textAlign: 'center'
    },
    stepTitle: {
        fontSize: '22px',
        fontWeight: '700',
        color: '#1a1a2e',
        margin: '0 0 8px'
    },
    stepSubtitle: {
        color: '#6b7280',
        fontSize: '14px',
        marginBottom: '32px'
    },
    tenantGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fill, minmax(160px, 1fr))',
        gap: '16px',
        marginBottom: '32px'
    },
    tenantCard: {
        backgroundColor: 'white',
        borderRadius: '12px',
        padding: '24px 16px',
        border: '2px solid #e5e7eb',
        cursor: 'pointer',
        textAlign: 'center',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: '8px'
    },
    tenantIcon: { fontSize: '32px' },
    tenantName: { fontWeight: '600', color: '#1a1a2e', fontSize: '14px' },
    tenantShop: { fontSize: '12px', color: '#4f46e5', fontWeight: '500' },
    noTenants: {
        padding: '32px',
        color: '#9ca3af',
        fontSize: '14px',
        backgroundColor: 'white',
        borderRadius: '12px'
    },
    loadingMsg: { color: '#6b7280', fontSize: '14px', marginTop: '16px' },
    demoNote: {
        backgroundColor: '#eff6ff',
        border: '1px solid #bfdbfe',
        borderRadius: '8px',
        padding: '16px',
        fontSize: '13px',
        color: '#1e40af',
        textAlign: 'left',
        lineHeight: '1.6'
    },
    shopLayout: {
        display: 'grid',
        gridTemplateColumns: '1fr 360px',
        gap: '24px',
        alignItems: 'start'
    },
    productsSection: {},
    productsGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fill, minmax(200px, 1fr))',
        gap: '16px'
    },
    productCard: {
        backgroundColor: 'white',
        borderRadius: '12px',
        padding: '20px',
        boxShadow: '0 1px 4px rgba(0,0,0,0.06)',
        display: 'flex',
        flexDirection: 'column',
        gap: '8px'
    },
    productImage: { fontSize: '40px', textAlign: 'center', padding: '8px 0' },
    productInfo: { flex: 1 },
    productName: { fontWeight: '600', color: '#1a1a2e', fontSize: '15px', marginBottom: '4px' },
    productSku: { fontSize: '11px', color: '#9ca3af', fontFamily: 'monospace', marginBottom: '4px' },
    productPrice: { fontSize: '20px', fontWeight: '700', color: '#4f46e5', marginBottom: '4px' },
    productStock: { fontSize: '12px', color: '#6b7280' },
    addToCartBtn: {
        padding: '10px',
        backgroundColor: '#4f46e5',
        color: 'white',
        border: 'none',
        borderRadius: '8px',
        fontSize: '14px',
        fontWeight: '600',
        cursor: 'pointer',
        marginTop: '8px'
    },
    qtyControl: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        gap: '12px',
        marginTop: '8px',
        backgroundColor: '#f3f4f6',
        borderRadius: '8px',
        padding: '6px'
    },
    qtyBtn: {
        width: '32px',
        height: '32px',
        backgroundColor: 'white',
        border: '1px solid #e5e7eb',
        borderRadius: '6px',
        fontSize: '18px',
        cursor: 'pointer',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        fontWeight: '600'
    },
    qtyNum: { fontWeight: '700', fontSize: '16px', minWidth: '24px', textAlign: 'center' },
    noProducts: {
        padding: '48px',
        textAlign: 'center',
        color: '#9ca3af',
        backgroundColor: 'white',
        borderRadius: '12px',
        fontSize: '14px',
        lineHeight: '1.6'
    },
    cartSection: {
        backgroundColor: 'white',
        borderRadius: '12px',
        padding: '20px',
        boxShadow: '0 1px 4px rgba(0,0,0,0.06)',
        position: 'sticky',
        top: '24px'
    },
    emptyCart: {
        textAlign: 'center',
        color: '#9ca3af',
        padding: '32px 0',
        fontSize: '14px'
    },
    emptyCartIcon: { fontSize: '40px', marginBottom: '8px' },
    cartContent: { display: 'flex', flexDirection: 'column', gap: '0' },
    cartItem: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        padding: '12px 0',
        borderBottom: '1px solid #f3f4f6'
    },
    cartItemLeft: {},
    cartItemName: { fontWeight: '500', fontSize: '14px', color: '#1a1a2e' },
    cartItemPrice: { fontSize: '12px', color: '#9ca3af' },
    cartItemRight: { display: 'flex', alignItems: 'center', gap: '8px' },
    cartItemQty: { fontSize: '13px', color: '#6b7280' },
    cartItemTotal: { fontWeight: '600', fontSize: '14px' },
    removeItem: {
        background: 'none',
        border: 'none',
        color: '#9ca3af',
        fontSize: '16px',
        cursor: 'pointer',
        padding: '0 4px'
    },
    cartTotal: {
        display: 'flex',
        justifyContent: 'space-between',
        padding: '16px 0',
        fontWeight: '600',
        fontSize: '16px',
        borderTop: '2px solid #e5e7eb',
        marginTop: '8px'
    },
    cartTotalAmount: { color: '#4f46e5', fontSize: '20px' },
    customerForm: { display: 'flex', flexDirection: 'column', gap: '8px', margin: '8px 0 16px' },
    formLabel: { fontSize: '13px', fontWeight: '500', color: '#374151' },
    formInput: {
        padding: '10px 12px',
        borderRadius: '8px',
        border: '1px solid #e5e7eb',
        fontSize: '14px',
        outline: 'none'
    },
    placeOrderBtn: {
        width: '100%',
        padding: '14px',
        backgroundColor: '#4f46e5',
        color: 'white',
        border: 'none',
        borderRadius: '8px',
        fontSize: '15px',
        fontWeight: '700',
        cursor: 'pointer'
    },
    successPage: {
        display: 'flex',
        justifyContent: 'center',
        alignItems: 'center',
        minHeight: '60vh'
    },
    successCard: {
        backgroundColor: 'white',
        borderRadius: '16px',
        padding: '48px',
        maxWidth: '480px',
        width: '100%',
        textAlign: 'center',
        boxShadow: '0 4px 24px rgba(0,0,0,0.08)'
    },
    successIcon: { fontSize: '64px', marginBottom: '16px' },
    successTitle: { fontSize: '28px', fontWeight: '700', color: '#1a1a2e', margin: '0 0 8px' },
    successSubtitle: { color: '#6b7280', fontSize: '16px', margin: '0 0 24px' },
    orderId: {
        backgroundColor: '#f3f4f6',
        borderRadius: '8px',
        padding: '12px 24px',
        fontFamily: 'monospace',
        fontSize: '18px',
        fontWeight: '700',
        color: '#1a1a2e',
        marginBottom: '24px',
        display: 'inline-block'
    },
    successSteps: {
        display: 'flex',
        flexDirection: 'column',
        gap: '12px',
        marginBottom: '24px',
        textAlign: 'left'
    },
    successStep: {
        display: 'flex',
        alignItems: 'center',
        gap: '12px',
        fontSize: '14px',
        color: '#374151'
    },
    stepIcon: { fontSize: '20px' },
    successNote: {
        backgroundColor: '#eff6ff',
        border: '1px solid #bfdbfe',
        borderRadius: '8px',
        padding: '16px',
        fontSize: '14px',
        color: '#1e40af',
        marginBottom: '24px',
        lineHeight: '1.6'
    },
    successActions: { display: 'flex', gap: '12px', justifyContent: 'center' },
    shopMoreBtn: {
        padding: '10px 20px',
        backgroundColor: '#f3f4f6',
        color: '#374151',
        border: 'none',
        borderRadius: '8px',
        fontSize: '14px',
        fontWeight: '600',
        cursor: 'pointer'
    },
    adminLoginBtn: {
        padding: '10px 20px',
        backgroundColor: '#4f46e5',
        color: 'white',
        borderRadius: '8px',
        fontSize: '14px',
        fontWeight: '600',
        textDecoration: 'none',
        display: 'inline-block'
    }
};
