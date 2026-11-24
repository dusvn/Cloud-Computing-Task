export const API = "http://localhost:8958/api/Client";
export const PURCHASES_API = "http://localhost:8958/api/purchases";

// Dusan (public.users) - the only user in this demo
export const USER = {
  userId: "6ed4b623-212b-4e29-bd75-b4b68a544532",
  firstName: "Dusan",
  lastName: "Paripovic",
  email: "example@example.com"
};

// Must stay identical to public.books (book_id, book_name, price).
export const BOOKS = [
  { bookId: "b0000000-0000-0000-0000-000000000001", name: "Knjiga1", label: "Knjiga 1", price: 10.26 },
  { bookId: "b0000000-0000-0000-0000-000000000002", name: "Knjiga2", label: "Knjiga 2", price: 26.10 },
  { bookId: "b0000000-0000-0000-0000-000000000003", name: "Knjiga3", label: "Knjiga 3", price: 15.24 },
  { bookId: "b0000000-0000-0000-0000-000000000004", name: "Knjiga4", label: "Knjiga 4", price: 12.50 },
  { bookId: "b0000000-0000-0000-0000-000000000005", name: "Knjiga5", label: "Knjiga 5", price: 8.99 },
  { bookId: "b0000000-0000-0000-0000-000000000006", name: "Knjiga6", label: "Knjiga 6", price: 19.90 },
  { bookId: "b0000000-0000-0000-0000-000000000007", name: "Knjiga7", label: "Knjiga 7", price: 22.00 },
  { bookId: "b0000000-0000-0000-0000-000000000008", name: "Knjiga8", label: "Knjiga 8", price: 5.75 }
];
