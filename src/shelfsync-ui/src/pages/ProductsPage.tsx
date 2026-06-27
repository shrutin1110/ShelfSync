import { useState, useEffect } from 'react';
import Layout from '../components/Layout';
import getClient from '../api/apolloClient';

// ── GraphQL operations ────────────────────────────────────────

const GET_PRODUCTS = `
  query {
    products {
      id
      name
      sku
      price
      stockQuantity
      isActive
      createdAt
    }
  }
`;

const ADD_PRODUCT = `
  mutation AddProduct($input: AddProductInput!) {
    addProduct(input: $input) {
      id
      name
      sku
      price
      stockQuantity
    }
  }
`;

const GET_UPLOAD_URL = `
  mutation GetUploadUrl($input: GetUploadUrlInput!) {
    productImageUploadUrl(input: $input) {
      uploadUrl
      s3Key
      expiresAt
    }
  }
`;

// ── Types ─────────────────────────────────────────────────────

interface Product {
    id: string;
    name: string;
    sku: string;
    price: number;
    stockQuantity: number;
    isActive: boolean;
    createdAt: string;
}

interface ProductForm {
    name: string;
    sku: string;
    price: string;
    initialStock: string;
}

// ── Main Component ────────────────────────────────────────────

export default function ProductsPage() {
    const [products, setProducts] = useState<Product[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [search, setSearch] = useState('');
    const [showForm, setShowForm] = useState(false);
    const [formLoading, setFormLoading] = useState(false);
    const [formError, setFormError] = useState('');
    const [selectedImage, setSelectedImage]
        = useState<File | null>(null);
    const [uploadingImage, setUploadingImage] = useState(false);
    const [successMessage, setSuccessMessage]
        = useState<string | null>(null);

    const [form, setForm] = useState<ProductForm>({
        name: '',
        sku: '',
        price: '',
        initialStock: ''
    });

    // Load products on page mount
    useEffect(() => {
        loadProducts();
    }, []);

    const loadProducts = async () => {
        try {
            setLoading(true);
            setError(null);
            const result = await getClient()
                .request<{ products: Product[] }>(GET_PRODUCTS);
            setProducts(result.products);
        } catch (err: any) {
            setError('Failed to load products. ' +
                'Make sure the Orders service is running.');
            console.error('Load products error:', err);
        } finally {
            setLoading(false);
        }
    };

    // Computed from state — not stored separately
    const filteredProducts = products.filter(p =>
        p.name.toLowerCase().includes(search.toLowerCase()) ||
        p.sku.toLowerCase().includes(search.toLowerCase())
    );

    const lowStockCount = products
        .filter(p => p.stockQuantity < 10).length;

    const handleAddProduct = async (e: React.FormEvent) => {
        e.preventDefault();
        setFormLoading(true);
        setFormError('');

        try {
            // Step 1 — Save product to database
            const result = await getClient()
                .request<{ addProduct: Product }>(ADD_PRODUCT, {
                    input: {
                        name: form.name,
                        sku: form.sku,
                        price: parseFloat(form.price),
                        initialStock: parseInt(form.initialStock)
                    }
                });

            const newProduct = result.addProduct;

            // Step 2 — Upload image if selected
            if (selectedImage) {
                setUploadingImage(true);
                await uploadProductImage(newProduct.id, selectedImage);
                setUploadingImage(false);
            }

            // Step 3 — Refresh list and reset form
            await loadProducts();
            setForm({ name: '', sku: '', price: '', initialStock: '' });
            setSelectedImage(null);
            setShowForm(false);
            setSuccessMessage(
                `"${newProduct.name}" added successfully!`);

            // Clear success message after 3 seconds
            setTimeout(() => setSuccessMessage(null), 3000);

        } catch (err: any) {
            setFormError(
                err?.message ?? 'Failed to add product. Please try again.');
        } finally {
            setFormLoading(false);
            setUploadingImage(false);
        }
    };

    const uploadProductImage = async (
        productId: string,
        file: File
    ) => {
        try {
            // Step 1 — Get presigned URL from your .NET API
            const ext = file.name.split('.').pop() ?? 'jpg';

            const urlResult = await getClient()
                .request<{
                    productImageUploadUrl: {
                        uploadUrl: string;
                        s3Key: string;
                    }
                }>(GET_UPLOAD_URL, {
                    input: { productId, fileExtension: ext }
                });

            const { uploadUrl } = urlResult.productImageUploadUrl;

            // Step 2 — Upload directly to S3
            // File goes: browser → S3
            // Your .NET server never touches the file
            const uploadResponse = await fetch(uploadUrl, {
                method: 'PUT',
                body: file,
                headers: { 'Content-Type': file.type }
            });

            if (!uploadResponse.ok) {
                throw new Error('S3 upload failed');
            }

            console.log('Image uploaded to S3 successfully');
        } catch (err) {
            console.error('Image upload failed:', err);
            // Image is optional — do not block product creation
        }
    };

    // ── Render ────────────────────────────────────────────────

    if (loading) return (
        <Layout>
            <div style={styles.container}>
                <div style={styles.loadingState}>
                    Loading products...
                </div>
            </div>
        </Layout>
    );

    if (error) return (
        <Layout>
            <div style={styles.container}>
                <div style={styles.errorState}>
                    <p>{error}</p>
                    <button
                        onClick={loadProducts}
                        style={styles.retryButton}
                    >
                        Retry
                    </button>
                </div>
            </div>
        </Layout>
    );

    return (
        <Layout>
            <div style={styles.container}>

                {/* Page Header */}
                <div style={styles.header}>
                    <div>
                        <h1 style={styles.title}>Products</h1>
                        <p style={styles.subtitle}>
                            {products.length} total
                            {lowStockCount > 0 && (
                                <span style={styles.lowStockBadge}>
                  {lowStockCount} low stock
                </span>
                            )}
                        </p>
                    </div>
                    <button
                        onClick={() => {
                            setShowForm(!showForm);
                            setFormError('');
                        }}
                        style={styles.addButton}
                    >
                        {showForm ? 'Cancel' : '+ Add Product'}
                    </button>
                </div>

                {/* Success message */}
                {successMessage && (
                    <div style={styles.success}>
                        {successMessage}
                    </div>
                )}

                {/* Add Product Form */}
                {showForm && (
                    <div style={styles.formCard}>
                        <h2 style={styles.formTitle}>Add New Product</h2>

                        {formError && (
                            <div style={styles.formError}>{formError}</div>
                        )}

                        <form onSubmit={handleAddProduct}>
                            <div style={styles.formGrid}>

                                <div style={styles.field}>
                                    <label style={styles.label}>
                                        Product Name *
                                    </label>
                                    <input
                                        style={styles.input}
                                        value={form.name}
                                        onChange={e => setForm({
                                            ...form, name: e.target.value
                                        })}
                                        placeholder="Red T-Shirt"
                                        required
                                    />
                                </div>

                                <div style={styles.field}>
                                    <label style={styles.label}>SKU *</label>
                                    <input
                                        style={styles.input}
                                        value={form.sku}
                                        onChange={e => setForm({
                                            ...form, sku: e.target.value
                                        })}
                                        placeholder="TSHIRT-RED-L"
                                        required
                                    />
                                </div>

                                <div style={styles.field}>
                                    <label style={styles.label}>Price ($) *</label>
                                    <input
                                        style={styles.input}
                                        type="number"
                                        step="0.01"
                                        min="0"
                                        value={form.price}
                                        onChange={e => setForm({
                                            ...form, price: e.target.value
                                        })}
                                        placeholder="29.99"
                                        required
                                    />
                                </div>

                                <div style={styles.field}>
                                    <label style={styles.label}>
                                        Initial Stock *
                                    </label>
                                    <input
                                        style={styles.input}
                                        type="number"
                                        min="0"
                                        value={form.initialStock}
                                        onChange={e => setForm({
                                            ...form, initialStock: e.target.value
                                        })}
                                        placeholder="100"
                                        required
                                    />
                                </div>

                            </div>

                            {/* Image upload */}
                            <div style={{ ...styles.field, marginTop: '16px' }}>
                                <label style={styles.label}>
                                    Product Image (optional)
                                </label>
                                <input
                                    type="file"
                                    accept="image/*"
                                    onChange={e =>
                                        setSelectedImage(
                                            e.target.files?.[0] ?? null
                                        )
                                    }
                                    style={styles.fileInput}
                                />
                                {selectedImage && (
                                    <p style={styles.fileSelected}>
                                        Selected: {selectedImage.name}
                                        ({(selectedImage.size / 1024).toFixed(1)} KB)
                                    </p>
                                )}
                            </div>

                            {/* Submit button */}
                            <div style={styles.formActions}>
                                <button
                                    type="submit"
                                    disabled={formLoading || uploadingImage}
                                    style={{
                                        ...styles.submitButton,
                                        opacity: formLoading ? 0.7 : 1
                                    }}
                                >
                                    {uploadingImage
                                        ? 'Uploading image...'
                                        : formLoading
                                            ? 'Saving...'
                                            : 'Add Product'}
                                </button>
                            </div>
                        </form>
                    </div>
                )}

                {/* Search bar */}
                <div style={styles.searchBar}>
                    <input
                        style={styles.searchInput}
                        value={search}
                        onChange={e => setSearch(e.target.value)}
                        placeholder="Search by name or SKU..."
                    />
                    {search && (
                        <button
                            onClick={() => setSearch('')}
                            style={styles.clearSearch}
                        >
                            Clear
                        </button>
                    )}
                </div>

                {/* Products table */}
                {filteredProducts.length === 0 ? (
                    <div style={styles.emptyState}>
                        {search
                            ? `No products matching "${search}"`
                            : 'No products yet. Click + Add Product to get started.'}
                    </div>
                ) : (
                    <div style={styles.tableCard}>
                        <table style={styles.table}>
                            <thead>
                            <tr style={styles.tableHeader}>
                                <th style={styles.th}>Product</th>
                                <th style={styles.th}>SKU</th>
                                <th style={styles.th}>Price</th>
                                <th style={styles.th}>Stock</th>
                                <th style={styles.th}>Status</th>
                                <th style={styles.th}>Added</th>
                            </tr>
                            </thead>
                            <tbody>
                            {filteredProducts.map((product, index) => (
                                <tr
                                    key={product.id}
                                    style={{
                                        ...styles.tableRow,
                                        backgroundColor: index % 2 === 0
                                            ? 'white'
                                            : '#f9fafb'
                                    }}
                                >
                                    {/* Product name */}
                                    <td style={styles.td}>
                                        <div style={styles.productName}>
                                            {product.name}
                                        </div>
                                    </td>

                                    {/* SKU */}
                                    <td style={styles.td}>
                                        <span style={styles.sku}>{product.sku}</span>
                                    </td>

                                    {/* Price */}
                                    <td style={styles.td}>
                      <span style={styles.price}>
                        ${product.price.toFixed(2)}
                      </span>
                                    </td>

                                    {/* Stock with warning */}
                                    <td style={styles.td}>
                      <span style={{
                          ...styles.stock,
                          color: product.stockQuantity < 10
                              ? '#ef4444'
                              : product.stockQuantity < 20
                                  ? '#f59e0b'
                                  : '#10b981'
                      }}>
                        {product.stockQuantity}
                          {product.stockQuantity < 10 && ' ⚠️'}
                      </span>
                                    </td>

                                    {/* Active status */}
                                    <td style={styles.td}>
                      <span style={{
                          ...styles.statusBadge,
                          backgroundColor: product.isActive
                              ? '#d1fae5'
                              : '#fee2e2',
                          color: product.isActive
                              ? '#065f46'
                              : '#991b1b'
                      }}>
                        {product.isActive ? 'Active' : 'Inactive'}
                      </span>
                                    </td>

                                    {/* Date added */}
                                    <td style={styles.td}>
                      <span style={styles.date}>
                        {new Date(product.createdAt)
                            .toLocaleDateString()}
                      </span>
                                    </td>
                                </tr>
                            ))}
                            </tbody>
                        </table>
                    </div>
                )}
            </div>
        </Layout>
    );
}

// ── Styles ────────────────────────────────────────────────────

const styles: Record<string, React.CSSProperties> = {
    container: {
        padding: '32px',
        maxWidth: '1200px'
    },
    header: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'flex-start',
        marginBottom: '24px'
    },
    title: {
        fontSize: '24px',
        fontWeight: '700',
        color: '#1a1a2e',
        margin: '0 0 4px'
    },
    subtitle: {
        color: '#666',
        fontSize: '14px',
        margin: 0,
        display: 'flex',
        alignItems: 'center',
        gap: '8px'
    },
    lowStockBadge: {
        backgroundColor: '#fee2e2',
        color: '#991b1b',
        padding: '2px 8px',
        borderRadius: '100px',
        fontSize: '12px',
        fontWeight: '500'
    },
    addButton: {
        padding: '10px 20px',
        backgroundColor: '#4f46e5',
        color: 'white',
        border: 'none',
        borderRadius: '8px',
        fontSize: '14px',
        fontWeight: '600',
        cursor: 'pointer'
    },
    success: {
        backgroundColor: '#d1fae5',
        color: '#065f46',
        padding: '12px 16px',
        borderRadius: '8px',
        marginBottom: '16px',
        fontSize: '14px',
        fontWeight: '500'
    },
    formCard: {
        backgroundColor: 'white',
        borderRadius: '12px',
        padding: '24px',
        marginBottom: '24px',
        boxShadow: '0 1px 4px rgba(0,0,0,0.08)'
    },
    formTitle: {
        fontSize: '16px',
        fontWeight: '600',
        color: '#1a1a2e',
        margin: '0 0 20px'
    },
    formError: {
        backgroundColor: '#fff0f0',
        border: '1px solid #ffcccc',
        borderRadius: '8px',
        padding: '12px',
        color: '#cc0000',
        fontSize: '14px',
        marginBottom: '16px'
    },
    formGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(2, 1fr)',
        gap: '16px'
    },
    field: {
        display: 'flex',
        flexDirection: 'column',
        gap: '6px'
    },
    label: {
        fontSize: '13px',
        fontWeight: '500',
        color: '#374151'
    },
    input: {
        padding: '10px 12px',
        borderRadius: '8px',
        border: '1px solid #e5e7eb',
        fontSize: '14px',
        outline: 'none'
    },
    fileInput: {
        padding: '8px 0',
        fontSize: '14px',
        color: '#374151'
    },
    fileSelected: {
        fontSize: '13px',
        color: '#4f46e5',
        margin: '4px 0 0'
    },
    formActions: {
        marginTop: '20px',
        display: 'flex',
        justifyContent: 'flex-end'
    },
    submitButton: {
        padding: '10px 24px',
        backgroundColor: '#4f46e5',
        color: 'white',
        border: 'none',
        borderRadius: '8px',
        fontSize: '14px',
        fontWeight: '600',
        cursor: 'pointer'
    },
    searchBar: {
        display: 'flex',
        gap: '8px',
        marginBottom: '16px',
        alignItems: 'center'
    },
    searchInput: {
        flex: 1,
        padding: '10px 14px',
        borderRadius: '8px',
        border: '1px solid #e5e7eb',
        fontSize: '14px',
        outline: 'none'
    },
    clearSearch: {
        padding: '10px 16px',
        backgroundColor: '#f3f4f6',
        border: 'none',
        borderRadius: '8px',
        fontSize: '14px',
        cursor: 'pointer',
        color: '#374151'
    },
    tableCard: {
        backgroundColor: 'white',
        borderRadius: '12px',
        boxShadow: '0 1px 4px rgba(0,0,0,0.06)',
        overflow: 'hidden'
    },
    table: {
        width: '100%',
        borderCollapse: 'collapse'
    },
    tableHeader: {
        backgroundColor: '#f9fafb'
    },
    th: {
        padding: '12px 16px',
        textAlign: 'left',
        fontSize: '12px',
        fontWeight: '600',
        color: '#6b7280',
        textTransform: 'uppercase',
        letterSpacing: '0.05em',
        borderBottom: '1px solid #e5e7eb'
    },
    tableRow: {
        borderBottom: '1px solid #f3f4f6',
        transition: 'background-color 0.1s'
    },
    td: {
        padding: '14px 16px',
        fontSize: '14px'
    },
    productName: {
        fontWeight: '500',
        color: '#1a1a2e'
    },
    sku: {
        fontFamily: 'monospace',
        fontSize: '13px',
        color: '#6b7280',
        backgroundColor: '#f3f4f6',
        padding: '2px 6px',
        borderRadius: '4px'
    },
    price: {
        fontWeight: '600',
        color: '#1a1a2e'
    },
    stock: {
        fontWeight: '600',
        fontSize: '14px'
    },
    statusBadge: {
        padding: '4px 10px',
        borderRadius: '100px',
        fontSize: '12px',
        fontWeight: '500'
    },
    date: {
        color: '#9ca3af',
        fontSize: '13px'
    },
    loadingState: {
        padding: '48px',
        textAlign: 'center',
        color: '#6b7280',
        fontSize: '16px'
    },
    errorState: {
        padding: '48px',
        textAlign: 'center',
        color: '#ef4444'
    },
    retryButton: {
        marginTop: '16px',
        padding: '8px 20px',
        backgroundColor: '#4f46e5',
        color: 'white',
        border: 'none',
        borderRadius: '8px',
        cursor: 'pointer'
    },
    emptyState: {
        padding: '48px',
        textAlign: 'center',
        color: '#9ca3af',
        fontSize: '16px',
        backgroundColor: 'white',
        borderRadius: '12px',
        boxShadow: '0 1px 4px rgba(0,0,0,0.06)'
    }
};