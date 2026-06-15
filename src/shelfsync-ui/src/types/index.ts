export interface AuthResponse {
    accessToken: string;
    refreshToken: string;
    email: string;
    role: string;
    tenantName: string;
}

export interface User {
    email: string;
    role: string;
    tenantName: string;
    tenantId: string;
}

export interface Product {
    id: string;
    name: string;
    sku: string;
    price: number;
    stockQuantity: number;
    isActive: boolean;
    createdAt: string;
}

export interface OrderItem {
    id: string;
    quantity: number;
    unitPrice: number;
    product: {
        name: string;
        sku: string;
    };
}

export interface Order {
    id: string;
    status: string;
    totalAmount: number;
    notes: string;
    createdAt: string;
    updatedAt: string;
    items: OrderItem[];
}