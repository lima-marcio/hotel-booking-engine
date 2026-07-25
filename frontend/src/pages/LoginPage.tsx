import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useNavigate } from "react-router-dom";
import { useAuth } from "../hooks/useAuth";

const loginSchema = z.object({
  username: z.string().min(1, "Username is required"),
  password: z.string().min(1, "Password is required"),
});

type LoginFormValues = z.infer<typeof loginSchema>;

export function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const [loginError, setLoginError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
  });

  async function onSubmit(values: LoginFormValues) {
    setLoginError(null);
    try {
      await login(values.username, values.password);
      navigate("/");
    } catch {
      setLoginError("Invalid username or password.");
    }
  }

  return (
    <main className="flex min-h-screen flex-col items-center justify-center gap-4">
      <h1 className="text-2xl font-semibold">Login</h1>
      <form onSubmit={handleSubmit(onSubmit)} className="flex w-full max-w-xs flex-col gap-3">
        <div>
          <label htmlFor="username" className="block text-sm font-medium">
            Username
          </label>
          <input
            id="username"
            type="text"
            className="w-full rounded border px-3 py-2"
            {...register("username")}
          />
          {errors.username && <p className="text-sm text-red-600">{errors.username.message}</p>}
        </div>
        <div>
          <label htmlFor="password" className="block text-sm font-medium">
            Password
          </label>
          <input
            id="password"
            type="password"
            className="w-full rounded border px-3 py-2"
            {...register("password")}
          />
          {errors.password && <p className="text-sm text-red-600">{errors.password.message}</p>}
        </div>
        {loginError && <p className="text-sm text-red-600">{loginError}</p>}
        <button
          type="submit"
          disabled={isSubmitting}
          className="rounded bg-blue-600 px-4 py-2 text-white disabled:opacity-50"
        >
          {isSubmitting ? "Signing in..." : "Sign in"}
        </button>
      </form>
    </main>
  );
}
